using System.Globalization;
using System.Text.Json;
using UkraineDigitalAid.Store;

namespace UkraineDigitalAid.History;

/// <summary>
/// Зрізи за датами, по файлу на день: snapshots/2026-11-18.json. Повторний запуск того
/// самого дня перезаписує файл дня, зрізи старші за KeepDays видаляються.
/// </summary>
public class SnapshotStore(string directory)
{
    public const int KeepDays = 35;

    /// <summary>
    /// Щотижневий збір іде щопонеділка, але запуск за розкладом у GitHub Actions іноді
    /// запізнюється на години, тож «тиждень тому» це зріз, старший щонайменше на 6 днів.
    /// </summary>
    public const int WeekDays = 6;

    // справжній зріз важить близько 55 КБ; більший файл з опублікованої сторінки не беремо
    public const long MaxImportBytes = 5 * 1024 * 1024;

    private const string DateFormat = "yyyy-MM-dd";

    public string Directory { get; } = directory;

    public IReadOnlyList<DateOnly> Dates()
    {
        if (!System.IO.Directory.Exists(Directory))
            return [];
        return System.IO.Directory.EnumerateFiles(Directory, "*.json")
            .Select(path => DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(path), DateFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : (DateOnly?)null)
            .OfType<DateOnly>()
            .Order()
            .ToList();
    }

    public Snapshot Load(DateOnly date) => Parse(File.ReadAllText(PathFor(date)));

    /// <summary>
    /// З чим порівнювати зріз дня today: найсвіжіший, старший щонайменше на тиждень.
    /// Якщо такого немає (кеш з'явився нещодавно), найстаріший з попередніх днів: так видно
    /// хоча б зміни за ці дні, а сторінка показує обидві дати.
    /// </summary>
    public Snapshot? Baseline(DateOnly today)
    {
        var earlier = Dates().Where(d => d < today).ToList();
        if (earlier.Count == 0)
            return null;
        var weekAgo = earlier.Where(d => d.DayNumber <= today.DayNumber - WeekDays).ToList();
        return Load(weekAgo.Count > 0 ? weekAgo[^1] : earlier[0]);
    }

    /// <summary>Записує зріз дня і прибирає ті, що старші за KeepDays від нього.</summary>
    public async Task SaveAsync(Snapshot snapshot, CancellationToken ct)
    {
        await WriteAsync(snapshot, ct);
        foreach (var date in Dates().Where(d => d.DayNumber < snapshot.Date.DayNumber - KeepDays))
            File.Delete(PathFor(date));
    }

    /// <summary>
    /// Додає зріз з файлу, якщо за його дату свого ще немає. Так збір після втрати кешу
    /// бере за попередній зріз той, що опубліковано на сторінці. Зріз з датою не раніше
    /// today або старший за KeepDays не потрібен для порівняння і не береться.
    /// </summary>
    public async Task<Snapshot?> ImportAsync(string path, DateOnly today, CancellationToken ct)
    {
        if (!File.Exists(path) || new FileInfo(path).Length > MaxImportBytes)
            return null;
        var snapshot = Parse(await File.ReadAllTextAsync(path, ct));
        if (snapshot.Date >= today || snapshot.Date.DayNumber < today.DayNumber - KeepDays || File.Exists(PathFor(snapshot.Date)))
            return null;
        await WriteAsync(snapshot, ct);
        return snapshot;
    }

    public static Snapshot Parse(string json)
    {
        var snapshot = JsonSerializer.Deserialize<Snapshot>(json, ActivityStore.Json);
        if (snapshot?.Activities is null || snapshot.Date == default || snapshot.Activities.Any(a => string.IsNullOrEmpty(a?.Id)))
            throw new JsonException("Файл не схожий на зріз переліку");
        return snapshot;
    }

    private async Task WriteAsync(Snapshot snapshot, CancellationToken ct)
    {
        System.IO.Directory.CreateDirectory(Directory);
        await AtomicFile.WriteTextAsync(PathFor(snapshot.Date), JsonSerializer.Serialize(snapshot, ActivityStore.Json), ct);
    }

    private string PathFor(DateOnly date) => Path.Combine(Directory, date.ToString(DateFormat, CultureInfo.InvariantCulture) + ".json");
}
