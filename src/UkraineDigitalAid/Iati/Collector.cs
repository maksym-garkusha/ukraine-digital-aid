using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using UkraineDigitalAid.Activities;
using UkraineDigitalAid.Selection;
using UkraineDigitalAid.Store;

namespace UkraineDigitalAid.Iati;

public record Collected(FetchRun Run, IReadOnlyCollection<CachedActivity> Entries);

/// <summary>
/// Збір у три кроки. Спершу короткий список усіх активностей з отримувачем Україна.
/// Потім кандидати: ІКТ-сектори, text_search за кожним словом і слова в назвах зі списку.
/// Повний запис забирається тільки для кандидатів, а точні правила застосовуються вже локально.
/// </summary>
public class Collector(DPortalClient portal, TextWriter log)
{
    public const int Parallel = 3;
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    private static readonly (string, string)[] Ukraine = [("from", "act,country"), ("country_code", ActivityParser.Ukraine)];

    public async Task<Collected> RunAsync(IReadOnlyDictionary<string, CachedActivity> cache, DateTime now, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();

        var rows = new Dictionary<string, ListRow>();
        int unreadable = 0;
        await foreach (var element in portal.ListAsync(Ukraine, ListRow.Columns, 2000, ct))
        {
            if (ListRow.TryRead(element) is { } row)
                rows.TryAdd(row.Aid, row);
            else
                unreadable++;
        }
        log.WriteLine($"У списку d-portal {rows.Count} активностей з отримувачем Україна, нерозбірних рядків: {unreadable}");

        var sources = new Dictionary<string, int>();
        var candidates = new HashSet<string>();

        foreach (string code in DigitalRules.SectorCodes)
        {
            var found = await AidsAsync([("from", "act,country,sector"), ("country_code", ActivityParser.Ukraine), ("sector_code", code)], ct);
            sources["sector " + code] = found.Count;
            candidates.UnionWith(found);
        }
        foreach (string term in Keywords.All.SelectMany(k => k.SearchTerms))
        {
            var found = await AidsAsync([.. Ukraine, ("text_search", term)], ct);
            sources["text " + term] = found.Count;
            candidates.UnionWith(found);
        }
        var byTitle = rows.Values.Where(r => r.Title is not null && Keywords.Find(r.Title).Any()).Select(r => r.Aid).ToList();
        sources["title"] = byTitle.Count;
        candidates.UnionWith(byTitle);
        log.WriteLine($"Кандидатів: {candidates.Count}");

        var entries = new ConcurrentBag<CachedActivity>();
        int downloaded = 0, fromCache = 0, notFound = 0, failed = 0, done = 0;
        await System.Threading.Tasks.Parallel.ForEachAsync(candidates.Order(StringComparer.Ordinal),
            new ParallelOptions { MaxDegreeOfParallelism = Parallel, CancellationToken = ct },
            async (aid, token) =>
            {
                rows.TryGetValue(aid, out var row);
                string fingerprint = row?.Fingerprint() ?? "";
                cache.TryGetValue(aid, out var cached);
                if (cached is not null && cached.Fingerprint == fingerprint && now - cached.FetchedAt < MaxAge)
                {
                    entries.Add(cached);
                    Interlocked.Increment(ref fromCache);
                }
                else
                {
                    try
                    {
                        var xson = await portal.GetActivityAsync(aid, token);
                        if (xson is { } record)
                        {
                            entries.Add(new CachedActivity(fingerprint, now, ActivityParser.Parse(record, row)));
                            Interlocked.Increment(ref downloaded);
                        }
                        else
                            Interlocked.Increment(ref notFound);
                    }
                    catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or FormatException
                                                  or KeyNotFoundException or InvalidOperationException
                                              && !token.IsCancellationRequested)
                    {
                        Interlocked.Increment(ref failed);
                        log.WriteLine($"Не вдалося забрати {aid}: {e.Message}");
                        // краще стара версія запису, ніж жодної
                        if (cached is not null)
                            entries.Add(cached);
                    }
                }
                int count = Interlocked.Increment(ref done);
                if (count % 100 == 0)
                    log.WriteLine($"  {count} з {candidates.Count}");
            });

        var run = new FetchRun(now, clock.Elapsed, rows.Count, candidates.Count, downloaded, fromCache, notFound, failed, sources);
        return new Collected(run, entries);
    }

    private async Task<HashSet<string>> AidsAsync(IReadOnlyList<(string, string)> filter, CancellationToken ct)
    {
        var result = new HashSet<string>();
        await foreach (var row in portal.ListAsync(filter, "aid", 1000, ct))
            if (row.GetProperty("aid").GetString() is { } aid)
                result.Add(aid);
        return result;
    }

    /// <summary>
    /// Чи можна довіряти новому збору. Якщо список або кандидати впали більш ніж
    /// удвічі проти попереднього разу, або не вдалося забрати понад десяту частину
    /// записів, то це радше збій d-portal, ніж зміна даних.
    /// </summary>
    public static string? Problem(FetchRun run, FetchRun? previous)
    {
        if (run.Listed == 0 || run.Candidates == 0)
            return "d-portal не повернув жодної активності";
        if (run.Failed > run.Candidates / 10)
            return $"не вдалося забрати {run.Failed} записів з {run.Candidates}";
        if (previous is not null && run.Listed < previous.Listed / 2)
            return $"у списку {run.Listed} активностей, минулого разу було {previous.Listed}";
        if (previous is not null && run.Candidates < previous.Candidates / 2)
            return $"кандидатів {run.Candidates}, минулого разу було {previous.Candidates}";
        return null;
    }
}
