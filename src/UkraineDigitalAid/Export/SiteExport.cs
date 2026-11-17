using System.Text.Json;
using UkraineDigitalAid.Activities;
using UkraineDigitalAid.Iati;
using UkraineDigitalAid.Selection;
using UkraineDigitalAid.Stats;
using UkraineDigitalAid.Store;

namespace UkraineDigitalAid.Export;

/// <param name="Title">Назва від донора. Якщо донор її приховав, тут його пояснення, а TitleWithheld = true.</param>
/// <param name="Implementers">Ключі організацій-виконавців, назви лежать у SiteData.Organisations.</param>
/// <param name="OtherImplementers">Виконавці, вказані лише категорією («Developing country-based NGO»).</param>
/// <param name="HiddenImplementers">Скільки виконавців без назви: донор її приховав або це, схоже, фізособа.</param>
/// <param name="Why">Що збіглося: «22040 50%» або слово; перша підстава головна.</param>
/// <param name="Military">Військова допомога: запис дотичний, хоч би що збіглося.</param>
/// <param name="Parent">Програма, частиною якої є запис, якщо вона теж у переліку.</param>
/// <param name="HasParts">Частини цієї програми є в переліку окремо, у підсумках вона не рахується.</param>
/// <param name="HasDirectParts">Те саме серед самих прямих записів: підсумки прямих рахуються окремо,
/// і програма, чиї частини лише дотичні, у них рахується.</param>
public record SiteActivity(
    string Id, string Title, bool TitleWithheld, string? Description, string Donor,
    IReadOnlyList<string> Funders, IReadOnlyList<string> Implementers,
    IReadOnlyList<string> OtherImplementers, int HiddenImplementers,
    DateOnly? Start, DateOnly? End, int? Status,
    decimal? Commitment, decimal? Spend, double? Ukraine, double Ict,
    string Basis, IReadOnlyList<string> Why, bool Direct, bool Military,
    string? Parent, bool HasParts, bool HasDirectParts);

public record SiteDonor(string Id, string Name);

public record SiteKeyword(string Text, string? Plural, KeywordKind Kind);

public record SiteData(
    DateTime GeneratedAt,
    DateOnly Snapshot,
    int Listed,
    int Candidates,
    IReadOnlyDictionary<string, string> Sectors,
    IReadOnlyList<SiteDonor> Donors,
    IReadOnlyDictionary<string, string> Organisations,
    IReadOnlyDictionary<string, Summary> Summary,
    IReadOnlyList<SiteKeyword> Keywords,
    IReadOnlyList<SiteActivity> Activities);

/// <summary>
/// Усе для сторінки одним JSON. Сторінка статична, зведення пораховані тут для двох
/// наборів: тільки прямі активності і прямі разом з дотичними.
/// </summary>
public static class SiteExport
{
    public const string FileName = "data.json";
    public const int DescriptionLength = 600;

    public static SiteData Build(IEnumerable<Activity> activities, FetchRun run, Codelists codelists, DateTime now)
    {
        var today = DateOnly.FromDateTime(run.FetchedAt);
        var selected = Stats.Stats.Select(activities);
        var direct = selected.Where(s => s.Selection.Direct).Select(s => s.Activity).ToList();
        var all = selected.Select(s => s.Activity).ToList();
        var ids = all.Select(a => a.Id).ToHashSet();
        var parents = Stats.Stats.Parents(all);
        var directParents = Stats.Stats.Parents(direct);

        var organisations = new Dictionary<string, string>();
        var summary = new Dictionary<string, Summary>
        {
            ["direct"] = Stats.Stats.Summarise(direct, today),
            ["all"] = Stats.Stats.Summarise(all, today)
        };
        foreach (var implementer in summary["all"].Implementers)
            organisations[implementer.Id] = implementer.Name;

        var rows = selected.Select(s => Row(s, ids, parents, directParents, organisations)).ToList();

        var sectorCodes = selected.SelectMany(s => s.Activity.Sectors).Select(s => s.Code)
            .Where(DigitalRules.SectorCodes.Contains).Distinct();
        var donors = all.GroupBy(a => a.Publisher.Ref ?? a.Publisher.Name ?? "")
            // донора без жодного запису в підсумках (лише програми з чужими частинами) у таблиці немає
            .Select(g => new SiteDonor(g.Key, summary["all"].Donors.FirstOrDefault(d => d.Id == g.Key)?.Name
                                              ?? Organisations.PublisherName(g.First().Publisher)))
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .ToList();

        return new SiteData(
            now, today, run.Listed, run.Candidates,
            sectorCodes.Order().ToDictionary(c => c, c => codelists.Sectors.GetValueOrDefault(c, c)),
            donors, organisations, summary,
            Selection.Keywords.All.Select(k => new SiteKeyword(k.Text, k.Plural, k.Kind)).ToList(),
            rows.OrderByDescending(r => r.End ?? DateOnly.MinValue).ThenBy(r => r.Id, StringComparer.Ordinal).ToList());
    }

    public static async Task<long> WriteAsync(SiteData data, string directory, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, FileName);
        await using (var file = File.Create(path))
            await JsonSerializer.SerializeAsync(file, data, ActivityStore.Json, ct);
        return new FileInfo(path).Length;
    }

    private static SiteActivity Row(SelectedActivity s, HashSet<string> ids, HashSet<string> parents,
        HashSet<string> directParents, Dictionary<string, string> organisations)
    {
        var a = s.Activity;
        var implementing = a.WithRole(OrgRole.Implementing).ToList();
        var keys = new List<string>();
        foreach (var p in implementing)
        {
            if (Organisations.Key(p) is not { } key || p.Name is null)
                continue;
            organisations.TryAdd(key, p.Name);
            if (!keys.Contains(key))
                keys.Add(key);
        }

        return new SiteActivity(
            a.Id, a.Title, ActivityParser.IsRedacted(a.Title), Shorten(a.Description), a.Publisher.Ref ?? a.Publisher.Name ?? "",
            a.WithRole(OrgRole.Funding).Select(p => p.Name).OfType<string>().Distinct()
                .Where(n => n != a.Publisher.Name).ToList(),
            keys,
            implementing.Where(p => Organisations.IsGeneric(p) && p.Name is not null).Select(p => p.Name!).Distinct().ToList(),
            implementing.Count(p => p.Name is null),
            a.Start, a.End, a.Status, a.Commitment, a.Spend, a.UkraineShare, s.Selection.IctShare,
            s.Selection.Basis.ToString().ToLowerInvariant(),
            s.Selection.Reasons.Select(r => r.What).ToList(),
            s.Selection.Direct,
            s.Selection.Military,
            a.ParentId is { } parent && ids.Contains(parent) ? parent : null,
            parents.Contains(a.Id),
            directParents.Contains(a.Id));
    }

    private static string? Shorten(string? text)
    {
        if (text is null || text.Length <= DescriptionLength)
            return text;
        int cut = text.LastIndexOf(' ', DescriptionLength);
        return text[..(cut > DescriptionLength / 2 ? cut : DescriptionLength)].TrimEnd(',', ';', ':', '.') + "…";
    }
}
