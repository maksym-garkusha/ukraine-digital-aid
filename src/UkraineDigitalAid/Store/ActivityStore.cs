using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using UkraineDigitalAid.Activities;

namespace UkraineDigitalAid.Store;

/// <param name="Fingerprint">Відбиток рядка зі списку d-portal на момент завантаження.</param>
public record CachedActivity(string Fingerprint, DateTime FetchedAt, Activity Activity);

/// <param name="Listed">Скільки активностей з отримувачем Україна було в списку d-portal.</param>
/// <param name="Sources">Скільки кандидатів дало кожне джерело: сектор, слово в text_search, назва.</param>
public record FetchRun(
    DateTime FetchedAt, TimeSpan Duration, int Listed, int Candidates,
    int Downloaded, int FromCache, int NotFound, int Failed,
    IReadOnlyDictionary<string, int> Sources);

/// <summary>
/// Кеш на диску: активності в JSON Lines, по запису в рядку, і підсумок останнього збору.
/// Номер версії в назві файлу міняється разом з моделлю, і тоді все забирається наново.
/// </summary>
public class ActivityStore(string directory)
{
    public const int Version = 2;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Directory { get; } = directory;

    private string ActivitiesPath => Path.Combine(Directory, $"activities.v{Version}.jsonl");

    private string RunPath => Path.Combine(Directory, "last-fetch.json");

    public string CodelistsDirectory => Path.Combine(Directory, "codelists");

    public Dictionary<string, CachedActivity> Load()
    {
        var result = new Dictionary<string, CachedActivity>();
        if (!File.Exists(ActivitiesPath))
            return result;
        foreach (string line in File.ReadLines(ActivitiesPath))
        {
            if (line.Length == 0)
                continue;
            var entry = JsonSerializer.Deserialize<CachedActivity>(line, Json)
                ?? throw new InvalidDataException($"Порожній запис у {ActivitiesPath}");
            result[entry.Activity.Id] = entry;
        }
        return result;
    }

    public async Task SaveAsync(IEnumerable<CachedActivity> entries, FetchRun run, CancellationToken ct)
    {
        System.IO.Directory.CreateDirectory(Directory);
        await AtomicFile.WriteAsync(ActivitiesPath, async file =>
        {
            await using var writer = new StreamWriter(file);
            foreach (var entry in entries.OrderBy(e => e.Activity.Id, StringComparer.Ordinal))
                await writer.WriteLineAsync(JsonSerializer.Serialize(entry, Json).AsMemory(), ct);
        });
        await AtomicFile.WriteTextAsync(RunPath, JsonSerializer.Serialize(run, Json), ct);
    }

    public FetchRun? LoadRun() =>
        File.Exists(RunPath) ? JsonSerializer.Deserialize<FetchRun>(File.ReadAllText(RunPath), Json) : null;
}
