using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UkraineDigitalAid.History;
using UkraineDigitalAid.Store;

namespace UkraineDigitalAid.Export;

/// <summary>
/// Стрічка Atom з тими самими змінами, що в блоці «Що змінилося» на сторінці: нові й
/// завершені записи між двома зрізами. Файл на мову, бо в стрічці немає перемикача.
/// </summary>
public static class ChangesFeed
{
    public const string PageUrl = "https://maksym-garkusha.github.io/ukraine-digital-aid/";
    public static readonly string[] Languages = ["uk", "en"];

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    private record Texts(
        string Title, string Since, string NoBaseline, string Added, string Ended,
        string Donor, string Implementers, string NoImplementer, string Withheld,
        string Direct, string Related, string Energy, string WorthChecking,
        string EndedByStatus, string EndedByDate, IReadOnlyDictionary<int, string> Statuses);

    private static readonly Dictionary<string, Texts> Text = new()
    {
        ["uk"] = new(
            "Цифровізація України в даних IATI: нові й завершені активності",
            "Зміни між зрізами d-portal від {0} і {1}.",
            "Попереднього зрізу ще немає, перші зміни з'являться після наступного щотижневого збору.",
            "Нова", "Завершилася",
            "Донор", "Виконавці", "виконавця не вказано", "Назву приховано донором",
            "пряма", "дотична", "енергетика", "енергетика, варто перевірити",
            "Статус став «{0}».", "Дата завершення {0} минула між зрізами.",
            new Dictionary<int, string>
            {
                [1] = "у підготовці",
                [2] = "у реалізації",
                [3] = "фінальна стадія",
                [4] = "закрито",
                [5] = "скасовано",
                [6] = "призупинено"
            }),
        ["en"] = new(
            "Ukraine's digital transformation in IATI data: new and ended activities",
            "Changes between the d-portal snapshots of {0} and {1}.",
            "There is no earlier snapshot yet; the first changes will appear after the next weekly run.",
            "New", "Ended",
            "Donor", "Implementers", "no implementer given", "Title withheld by the donor",
            "direct", "related", "energy", "energy, worth checking",
            "Status changed to \"{0}\".", "End date {0} passed between the snapshots.",
            new Dictionary<int, string>
            {
                [1] = "Pipeline",
                [2] = "Implementation",
                [3] = "Finalisation",
                [4] = "Closed",
                [5] = "Cancelled",
                [6] = "Suspended"
            })
    };

    public static string FileName(string lang) => lang == "en" ? "changes.en.atom" : "changes.atom";

    public static async Task WriteAsync(SiteData data, string directory, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        foreach (string lang in Languages)
            await AtomicFile.WriteTextAsync(Path.Combine(directory, FileName(lang)), Build(data, lang), ct);
    }

    public static string Build(SiteData data, string lang)
    {
        var t = Text[lang];
        var culture = CultureInfo.GetCultureInfo(lang == "en" ? "en-GB" : "uk-UA");
        string feedUrl = PageUrl + FileName(lang);
        string Day(DateOnly d) => d.ToString(lang == "en" ? "d MMMM yyyy" : "dd.MM.yyyy", culture);

        var feed = new XElement(Atom + "feed",
            new XAttribute(XNamespace.Xml + "lang", lang),
            new XElement(Atom + "id", feedUrl),
            new XElement(Atom + "title", t.Title),
            new XElement(Atom + "subtitle", data.Changes is { } c ? string.Format(culture, t.Since, Day(c.From), Day(c.To)) : t.NoBaseline),
            new XElement(Atom + "updated", Stamp(data.GeneratedAt)),
            new XElement(Atom + "link", new XAttribute("rel", "self"), new XAttribute("href", feedUrl)),
            new XElement(Atom + "link", new XAttribute("rel", "alternate"), new XAttribute("type", "text/html"),
                new XAttribute("href", lang == "en" ? PageUrl + "#lang=en" : PageUrl)),
            new XElement(Atom + "author", new XElement(Atom + "name", "ukraine-digital-aid")));

        if (data.Changes is { } changes)
        {
            var byId = data.Activities.ToDictionary(a => a.Id);
            string updated = Stamp(changes.To.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            foreach (string id in changes.Added)
                feed.Add(Entry(data, byId[id], "added", t.Added, null, updated, t, culture));
            foreach (var ended in changes.Ended)
            {
                var a = byId[ended.Id];
                string why = ended.Reason == EndReason.Status
                    ? string.Format(culture, t.EndedByStatus, t.Statuses.GetValueOrDefault(a.Status ?? 0, a.Status?.ToString(culture) ?? ""))
                    : string.Format(culture, t.EndedByDate, a.End is { } end ? Day(end) : "");
                feed.Add(Entry(data, a, "ended", t.Ended, why, updated, t, culture));
            }
        }

        var xml = new StringBuilder();
        using (var writer = XmlWriter.Create(xml, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true }))
            new XDocument(feed).Save(writer);
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + xml + "\n";
    }

    private static XElement Entry(SiteData data, SiteActivity a, string kind, string label, string? why, string updated,
        Texts t, CultureInfo culture)
    {
        string title = a.TitleWithheld ? t.Withheld : a.Title;
        var implementers = a.Implementers.Select(k => data.Organisations.GetValueOrDefault(k)).OfType<string>()
            .Concat(a.OtherImplementers).ToList();
        string donor = data.Donors.FirstOrDefault(d => d.Id == a.Donor)?.Name ?? a.Donor;
        string from = a.Start?.Year.ToString(culture) ?? "…", to = a.End?.Year.ToString(culture) ?? "…";
        string years = from == to ? from : $"{from}–{to}";
        string status = a.Status is { } s ? t.Statuses.GetValueOrDefault(s, s.ToString(culture)) : "";
        string kindText = a.Direct ? t.Direct : t.Related;
        if (a.Energy is { } energy)
            kindText += "; " + (energy.WorthChecking ? t.WorthChecking : t.Energy);

        var summary = new StringBuilder();
        if (why is not null)
            summary.Append(why).Append(' ');
        summary.Append($"{t.Donor}: {donor}. ");
        summary.Append($"{t.Implementers}: {(implementers.Count > 0 ? string.Join("; ", implementers) : t.NoImplementer)}. ");
        summary.Append($"{years}, {status}. {char.ToUpper(kindText[0], culture)}{kindText[1..]}.");

        var entry = new XElement(Atom + "entry",
            new XElement(Atom + "id", $"tag:maksym-garkusha.github.io,2026:ukraine-digital-aid/{kind}/{Uri.EscapeDataString(a.Id)}"),
            new XElement(Atom + "title", Clean($"{label}: {title}")),
            new XElement(Atom + "updated", updated),
            new XElement(Atom + "link", new XAttribute("href", "https://d-portal.org/ctrack.html#view=act&aid=" + Uri.EscapeDataString(a.Id))),
            new XElement(Atom + "summary", Clean(summary.ToString())),
            new XElement(Atom + "category", new XAttribute("term", a.Direct ? "direct" : "related")));
        if (a.Energy is not null)
            entry.Add(new XElement(Atom + "category", new XAttribute("term", "energy")));
        return entry;
    }

    private static string Stamp(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    // донори часом лишають у назвах керівні символи, яких XML 1.0 не допускає, і запис стрічки впав би
    private static string Clean(string text)
    {
        var result = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (XmlConvert.IsXmlChar(text[i]))
                result.Append(text[i]);
            else if (i + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[i + 1], text[i]))
                result.Append(text[i]).Append(text[++i]);
        }
        return result.ToString();
    }
}
