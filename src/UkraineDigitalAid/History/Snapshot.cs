namespace UkraineDigitalAid.History;

/// <summary>Стан відібраного запису на дату зрізу: лише те, з чого видно появу і завершення.</summary>
public record SnapshotEntry(string Id, int? Status, DateOnly? End, bool Direct);

/// <summary>
/// Компактний зріз переліку. Лежить у кеші збору (cache/snapshots) і публікується на Pages
/// як snapshot.json, щоб наступний збір мав з чим порівнювати, навіть якщо кеш зник.
/// </summary>
public record Snapshot(DateOnly Date, IReadOnlyList<SnapshotEntry> Activities);

public enum EndReason
{
    /// <summary>Статус став «фінальна стадія», «закрито» або «скасовано».</summary>
    Status,
    /// <summary>Статус той самий, але між зрізами минула дата завершення.</summary>
    EndDate
}

public record Ended(string Id, EndReason Reason);

/// <param name="Added">Нові в переліку: донор щойно опублікував запис, або той почав підпадати під правила відбору.</param>
/// <param name="Gone">Були в попередньому зрізі, а тепер їх немає: донор зняв запис або той перестав підпадати під правила.</param>
public record Changes(
    DateOnly From, DateOnly To,
    IReadOnlyList<string> Added, IReadOnlyList<Ended> Ended, IReadOnlyList<SnapshotEntry> Gone);

public static class SnapshotDiff
{
    // довідник ActivityStatus: 3 фінальна стадія, 4 закрито, 5 скасовано
    public static bool IsOver(int? status) => status is 3 or 4 or 5;

    public static Changes Compare(Snapshot previous, Snapshot current)
    {
        var before = new Dictionary<string, SnapshotEntry>();
        foreach (var entry in previous.Activities)
            before.TryAdd(entry.Id, entry);
        var now = current.Activities.Select(a => a.Id).ToHashSet();

        var added = new List<string>();
        var ended = new List<Ended>();
        foreach (var entry in current.Activities.OrderBy(a => a.Id, StringComparer.Ordinal))
        {
            if (!before.TryGetValue(entry.Id, out var was))
                added.Add(entry.Id);
            else if (!IsOver(was.Status) && IsOver(entry.Status))
                ended.Add(new Ended(entry.Id, EndReason.Status));
            else if (!IsOver(entry.Status) && entry.End >= previous.Date && entry.End < current.Date)
                ended.Add(new Ended(entry.Id, EndReason.EndDate));
        }
        var gone = before.Values.Where(e => !now.Contains(e.Id)).OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
        return new Changes(previous.Date, current.Date, added, ended, gone);
    }
}
