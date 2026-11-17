using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using UkraineDigitalAid.Iati;

namespace UkraineDigitalAid.Activities;

/// <summary>
/// Розбирає повний запис IATI з d-portal. Блоки contact-info, result, budget,
/// document-link не читаються зовсім: перший містить імена й телефони людей,
/// решта для відбору не потрібні.
/// </summary>
public static partial class ActivityParser
{
    public const string Ukraine = "UA";

    public static Activity Parse(JsonElement xson, ListRow? row = null)
    {
        string id = Xson.String(xson, "/iati-identifier")?.Trim() ?? row?.Aid
            ?? throw new FormatException("У записі немає iati-identifier");
        string lang = Xson.Lang(xson) ?? "en";

        var (start, end) = Dates(xson);
        var related = Xson.Values(xson, "/related-activity")
            .Select(r => (Type: Xson.String(r, "@type"), Ref: Xson.String(r, "@ref")?.Trim()))
            .Where(r => !string.IsNullOrEmpty(r.Ref) && r.Ref != id)
            .ToList();

        return new Activity(
            id,
            Xson.Narratives(xson, "/title/narrative", lang),
            Descriptions(xson, lang),
            new Organisation(
                Xson.String(xson, "/reporting-org@ref")?.Trim() ?? row?.ReportingRef,
                Name(Xson.Narratives(xson, "/reporting-org/narrative", lang)) ?? row?.Reporting),
            Participants(xson, lang),
            start ?? row?.Start,
            end ?? row?.End,
            Code(Xson.String(xson, "/activity-status@code")) ?? row?.StatusCode,
            row?.Commitment,
            row?.Spend,
            Sectors(xson),
            UkraineShare(xson),
            related.FirstOrDefault(r => r.Type == "1").Ref,
            related.Where(r => r.Type == "2").Select(r => r.Ref!).Distinct().ToList());
    }

    private static List<Narrative> Descriptions(JsonElement xson, string lang)
    {
        // тип 1 загальний опис, 2 цілі, 3 цільові групи, 4 інше; загальний ставимо першим
        var items = Xson.Values(xson, "/description")
            .OrderBy(d => Xson.String(d, "@type") is null or "1" ? 0 : 1);
        var result = new List<Narrative>();
        foreach (var item in items)
            foreach (var n in Xson.Narratives(item, "/narrative", Xson.Lang(item) ?? lang))
            {
                string text = HideEmails(n.Text);
                if (!result.Any(r => r.Text == text))
                    result.Add(n with { Text = text });
            }
        return result;
    }

    private static List<Participant> Participants(JsonElement xson, string lang)
    {
        var result = new List<Participant>();
        foreach (var org in Xson.Values(xson, "/participating-org"))
        {
            if (Code(Xson.String(org, "@role")) is not { } role || role is < 1 or > 4)
                continue;
            string? reference = Xson.String(org, "@ref")?.Trim();
            string? name = Name(Xson.Narratives(org, "/narrative", Xson.Lang(org) ?? lang));
            if (name is not null && (IsRedacted(name) || PersonNames.LooksLikePerson(name, reference)))
                name = null;
            var participant = new Participant((OrgRole)role, reference, name, Xson.String(org, "@type"),
                reference is not null && IsCategoryCode(reference));
            if (!result.Contains(participant))
                result.Add(participant);
        }
        return result;
    }

    /// <summary>
    /// Початок і кінець: фактична дата, якщо є, інакше планова.
    /// Типи за довідником ActivityDateType: 1 і 3 планові, 2 і 4 фактичні.
    /// </summary>
    private static (DateOnly? Start, DateOnly? End) Dates(JsonElement xson)
    {
        var dates = new Dictionary<string, DateOnly>();
        foreach (var d in Xson.Values(xson, "/activity-date"))
            if (Xson.String(d, "@type") is { } type && ParseDate(Xson.String(d, "@iso-date")) is { } date)
                dates.TryAdd(type, date);
        return (Pick(dates, "2", "1"), Pick(dates, "4", "3"));
    }

    private static DateOnly? Pick(Dictionary<string, DateOnly> dates, string actual, string planned) =>
        dates.TryGetValue(actual, out var a) ? a : dates.TryGetValue(planned, out var p) ? p : null;

    public static DateOnly? ParseDate(string? iso) =>
        iso is { Length: >= 10 }
        && DateOnly.TryParseExact(iso[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date : null;

    /// <summary>
    /// Сектори DAC (словник 1). Якщо на рівні активності їх немає, частки рахуються
    /// з транзакцій за сумами, як це робить d-portal: так публікують USAID і частина записів Sida.
    /// </summary>
    public static List<SectorShare> Sectors(JsonElement xson)
    {
        var declared = Xson.Values(xson, "/sector")
            .Where(IsDac)
            .Select(s => (Code: Xson.String(s, "@code")!.Trim(), Percent: Xson.Number(s, "@percentage")))
            .Distinct()
            .ToList();
        if (declared.Count > 0)
        {
            double fallback = 100.0 / declared.Count;
            return Normalize(declared.Select(s => (s.Code, s.Percent ?? fallback)));
        }

        var weighted = new List<(string Code, double Weight)>();
        foreach (var tx in Xson.Values(xson, "/transaction"))
        {
            var codes = Xson.Values(tx, "/sector").Where(IsDac)
                .Select(s => Xson.String(s, "@code")!.Trim()).Distinct().ToList();
            double value = Math.Abs(Xson.Number(tx, "/value") ?? 0);
            foreach (string code in codes)
                weighted.Add((code, value / codes.Count));
        }
        double total = weighted.Sum(w => w.Weight);
        return total > 0 ? Normalize(weighted.Select(w => (w.Code, w.Weight * 100 / total))) : [];
    }

    private static bool IsDac(JsonElement sector) =>
        Xson.String(sector, "@vocabulary") is null or "1" or "DAC"
        && Xson.String(sector, "@code")?.Trim() is { Length: 5 } code && code.All(char.IsAsciiDigit);

    // частки одного коду додаються; якщо разом виходить понад 100 (донор продублював сектори), зводимо до 100
    private static List<SectorShare> Normalize(IEnumerable<(string Code, double Percent)> shares)
    {
        var merged = shares.GroupBy(s => s.Code).Select(g => (Code: g.Key, Percent: g.Sum(s => s.Percent))).ToList();
        double sum = merged.Sum(s => s.Percent);
        double scale = sum > 100.5 ? 100 / sum : 1;
        return merged
            .Select(s => new SectorShare(s.Code, Math.Round(s.Percent * scale, 2)))
            .OrderByDescending(s => s.Percent).ThenBy(s => s.Code)
            .ToList();
    }

    /// <summary>
    /// Частка України. Одна країна без відсотка означає 100. Якщо країн кілька, а
    /// відсотків немає, частку не вгадуємо. Якщо України серед країн активності немає, дивимось у транзакції.
    /// </summary>
    public static double? UkraineShare(JsonElement xson)
    {
        var countries = Xson.Values(xson, "/recipient-country")
            .Select(c => (Code: Xson.String(c, "@code")?.Trim().ToUpperInvariant(), Percent: Xson.Number(c, "@percentage")))
            .ToList();
        int regions = Xson.Values(xson, "/recipient-region").Count();

        var ukraine = countries.Where(c => c.Code == Ukraine).ToList();
        if (ukraine.Count > 0)
        {
            if (ukraine[0].Percent is { } percent)
                return Math.Min(percent, 100);
            return countries.Count == 1 && regions == 0 ? 100 : null;
        }

        double all = 0, toUkraine = 0;
        foreach (var tx in Xson.Values(xson, "/transaction"))
        {
            if (Xson.String(tx, "/recipient-country@code") is not { } code)
                continue;
            double value = Math.Abs(Xson.Number(tx, "/value") ?? 0);
            all += value;
            if (code.Trim().Equals(Ukraine, StringComparison.OrdinalIgnoreCase))
                toUkraine += value;
        }
        return all > 0 ? Math.Round(toUkraine * 100 / all, 2) : null;
    }

    /// <summary>
    /// Категорії каналів DAC на кшталт 47000 «Other multilateral institution» або
    /// 61000 «Private sector in provider country»: це не організація, а її тип.
    /// </summary>
    public static bool IsCategoryCode(string reference) => CategoryCode().IsMatch(reference);

    public static bool IsRedacted(string name) => name.Contains("redacted", StringComparison.OrdinalIgnoreCase);

    public static string HideEmails(string text) => Email().Replace(text, "[e-mail]");

    private static string? Name(List<Narrative> names) => Narratives.Preferred(names);

    private static int? Code(string? value) => int.TryParse(value, out int code) ? code : null;

    [GeneratedRegex(@"^(XM-DAC-)?\d{2}000$")]
    private static partial Regex CategoryCode();

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+")]
    private static partial Regex Email();
}
