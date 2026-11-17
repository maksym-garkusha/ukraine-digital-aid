using UkraineDigitalAid.Activities;
using UkraineDigitalAid.Selection;

namespace UkraineDigitalAid.Stats;

public record SelectedActivity(Activity Activity, Selection.Selection Selection);

/// <param name="WholeCommitment">Зобов'язання тільки за записами, де Україна 100%.</param>
/// <param name="WholeCount">Скільки таких записів.</param>
public record DonorStats(
    string Id, string Name, int Total, int Active,
    decimal WholeCommitment, decimal WholeSpend, int WholeCount);

public record ImplementerStats(string Id, string Name, int Activities, IReadOnlyList<string> Donors);

/// <param name="Counted">Скільки записів у підсумках: програми, частини яких теж відібрано, не рахуються.</param>
/// <param name="EndingSoon">У реалізації і завершуються протягом 12 місяців від дати зрізу.</param>
/// <param name="ActivePastEnd">Статус «у реалізації», хоча дата завершення вже минула.</param>
public record Summary(
    int Counted, int Active, int EndingSoon, int ActivePastEnd,
    IReadOnlyList<DonorStats> Donors,
    IReadOnlyList<ImplementerStats> Implementers,
    IReadOnlyDictionary<int, int> ByStatus,
    IReadOnlyDictionary<int, int> ByEndYear);

public static class Stats
{
    public const int Implementation = 2;

    public static List<SelectedActivity> Select(IEnumerable<Activity> activities) =>
        activities
            .Select(a => (Activity: a, Selection: DigitalRules.Evaluate(a)))
            .Where(x => x.Selection is not null)
            .Select(x => new SelectedActivity(x.Activity, x.Selection!))
            .OrderBy(x => x.Activity.Id, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Ідентифікатори програм, частини яких теж є в наборі. Донори (ЄС, Sida, FCDO)
    /// публікують і програму, і її частини окремими записами з тими самими грошима,
    /// тож у підсумках рахуємо тільки частини.
    /// </summary>
    public static HashSet<string> Parents(IReadOnlyCollection<Activity> set)
    {
        var ids = set.Select(a => a.Id).ToHashSet();
        var parents = new HashSet<string>();
        foreach (var activity in set)
        {
            if (activity.ParentId is { } parent && ids.Contains(parent))
                parents.Add(parent);
            if (activity.ChildIds.Any(ids.Contains))
                parents.Add(activity.Id);
        }
        return parents;
    }

    public static bool IsEndingSoon(Activity a, DateOnly today) =>
        a.Status == Implementation && a.End is { } end && end >= today && end <= today.AddYears(1);

    public static bool IsWhollyUkraine(Activity a) => a.UkraineShare >= 99.995;

    public static Summary Summarise(IReadOnlyCollection<Activity> set, DateOnly today)
    {
        var parents = Parents(set);
        var counted = set.Where(a => !parents.Contains(a.Id)).ToList();

        var donors = counted
            .GroupBy(a => a.Publisher.Ref ?? a.Publisher.Name ?? "")
            .Select(g =>
            {
                var whole = g.Where(IsWhollyUkraine).ToList();
                return new DonorStats(
                    g.Key, MostCommon(g.Select(a => Organisations.PublisherName(a.Publisher))),
                    g.Count(), g.Count(a => a.Status == Implementation),
                    whole.Sum(a => a.Commitment ?? 0), whole.Sum(a => a.Spend ?? 0), whole.Count);
            })
            .OrderByDescending(d => d.Total).ThenBy(d => d.Name, StringComparer.Ordinal)
            .ToList();

        var implementers = counted
            .SelectMany(a => a.WithRole(OrgRole.Implementing)
                .Select(p => (Key: Organisations.Key(p), p.Name, Activity: a))
                .Where(x => x.Key is not null && x.Name is not null)
                .DistinctBy(x => x.Key))
            .GroupBy(x => x.Key!)
            .Select(g => new ImplementerStats(
                g.Key, MostCommon(g.Select(x => x.Name!)), g.Count(),
                g.Select(x => x.Activity.Publisher.Ref ?? "").Distinct().Order(StringComparer.Ordinal).ToList()))
            .OrderByDescending(i => i.Activities).ThenBy(i => i.Name, StringComparer.Ordinal)
            .ToList();

        return new Summary(
            counted.Count,
            counted.Count(a => a.Status == Implementation),
            counted.Count(a => IsEndingSoon(a, today)),
            counted.Count(a => a.Status == Implementation && a.End < today),
            donors,
            implementers,
            Histogram(counted.Where(a => a.Status is not null).Select(a => a.Status!.Value)),
            Histogram(counted.Where(a => a.End is not null).Select(a => a.End!.Value.Year)));
    }

    private static Dictionary<int, int> Histogram(IEnumerable<int> values) =>
        values.GroupBy(v => v).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());

    // найчастіша назва; за рівності коротша, щоб «UNDP» не програвав довгому склеєному варіанту
    private static string MostCommon(IEnumerable<string> names) =>
        names.GroupBy(n => n.Trim())
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Length).ThenBy(g => g.Key, StringComparer.Ordinal)
            .First().Key;
}
