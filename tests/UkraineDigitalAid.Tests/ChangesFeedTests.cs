using System.Xml.Linq;
using UkraineDigitalAid.Activities;
using UkraineDigitalAid.Export;
using UkraineDigitalAid.History;
using UkraineDigitalAid.Iati;
using UkraineDigitalAid.Store;

namespace UkraineDigitalAid.Tests;

public class ChangesFeedTests
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    private static readonly FetchRun Run = new(
        new DateTime(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc), TimeSpan.Zero, 23855, 1196, 0, 0, 0, 0,
        new Dictionary<string, int>());

    private static readonly Codelists Lists = new(new Dictionary<string, string>(), new Dictionary<string, string>());

    private static readonly Snapshot Previous = new(new DateOnly(2026, 11, 11),
    [
        new SnapshotEntry("SE-0-1", 2, new DateOnly(2027, 6, 30), true),
        new SnapshotEntry("CH-4-2", 2, new DateOnly(2026, 11, 14), true)
    ]);

    private static XElement Feed(string lang, Snapshot? previous, params Activity[] activities) =>
        XElement.Parse(ChangesFeed.Build(SiteExport.Build(activities, Run, Lists, Run.FetchedAt, previous), lang));

    private static Activity[] ThisWeek() =>
    [
        Build.Activity(id: "SE-0-1", title: "Digital skills", status: 4, end: new DateOnly(2027, 6, 30)),
        Build.Activity(id: "CH-4-2", title: "E-governance for accountability", end: new DateOnly(2026, 11, 14)),
        Build.Activity(id: "DE-1-3", title: "Digital metering for the electricity grid", sectors: [new SectorShare("23630", 100)],
            participants: [Build.Implementer("Deutsche Gesellschaft für Internationale Zusammenarbeit (GIZ) GmbH", "XM-DAC-5-52")])
    ];

    [Fact]
    public void Feed_has_an_entry_for_every_new_and_ended_record()
    {
        var entries = Feed("uk", Previous, ThisWeek()).Elements(Atom + "entry").ToList();

        Assert.Equal(
            [
                "tag:maksym-garkusha.github.io,2026:ukraine-digital-aid/added/DE-1-3",
                "tag:maksym-garkusha.github.io,2026:ukraine-digital-aid/ended/CH-4-2",
                "tag:maksym-garkusha.github.io,2026:ukraine-digital-aid/ended/SE-0-1"
            ],
            entries.Select(e => (string)e.Element(Atom + "id")!));
        Assert.All(entries, e => Assert.Equal("2026-11-18T00:00:00Z", (string)e.Element(Atom + "updated")!));
    }

    [Fact]
    public void Entry_names_the_change_links_to_d_portal_and_lists_implementers()
    {
        var entry = Feed("uk", Previous, ThisWeek()).Elements(Atom + "entry").First();

        Assert.Equal("Нова: Digital metering for the electricity grid", (string)entry.Element(Atom + "title")!);
        Assert.Equal("https://d-portal.org/ctrack.html#view=act&aid=DE-1-3", (string)entry.Element(Atom + "link")!.Attribute("href")!);
        Assert.Contains("Виконавці: Deutsche Gesellschaft für Internationale Zusammenarbeit (GIZ) GmbH.", (string)entry.Element(Atom + "summary")!);
        Assert.Equal(["direct", "energy"], entry.Elements(Atom + "category").Select(c => (string)c.Attribute("term")!));
    }

    [Fact]
    public void Ended_entry_says_why()
    {
        var entries = Feed("en", Previous, ThisWeek()).Elements(Atom + "entry").ToDictionary(e => (string)e.Element(Atom + "title")!);

        Assert.StartsWith("End date 14 November 2026 passed", (string)entries["Ended: E-governance for accountability"].Element(Atom + "summary")!);
        Assert.StartsWith("Status changed to \"Closed\".", (string)entries["Ended: Digital skills"].Element(Atom + "summary")!);
    }

    [Fact]
    public void Without_a_previous_snapshot_the_feed_is_valid_and_empty()
    {
        var feed = Feed("uk", null, ThisWeek());

        Assert.Empty(feed.Elements(Atom + "entry"));
        Assert.Contains("Попереднього зрізу ще немає", (string)feed.Element(Atom + "subtitle")!);
        Assert.Equal("https://maksym-garkusha.github.io/ukraine-digital-aid/changes.atom", (string)feed.Element(Atom + "id")!);
    }

    [Fact]
    public void English_feed_points_to_the_English_page()
    {
        var feed = Feed("en", Previous, ThisWeek());

        var alternate = feed.Elements(Atom + "link").Single(l => (string?)l.Attribute("rel") == "alternate");
        Assert.Equal("https://maksym-garkusha.github.io/ukraine-digital-aid/#lang=en", (string)alternate.Attribute("href")!);
        Assert.Equal("https://maksym-garkusha.github.io/ukraine-digital-aid/changes.en.atom", (string)feed.Element(Atom + "id")!);
    }

    [Fact]
    public void Withheld_title_and_control_characters_do_not_reach_the_feed()
    {
        var feed = ChangesFeed.Build(SiteExport.Build(
            [
                Build.Activity(id: "US-GOV-1-1", title: "USAID redacted this field in accordance with the exceptions outlined in FATAA", sectors: [new SectorShare("22040", 100)]),
                Build.Activity(id: "SE-0-4", title: "Digital\u0001 services")
            ],
            Run, Lists, Run.FetchedAt, new Snapshot(new DateOnly(2026, 11, 11), [])), "uk");

        Assert.Contains("Нова: Назву приховано донором", feed);
        Assert.Contains("Нова: Digital services", feed);
        Assert.DoesNotContain("redacted", feed);
    }
}
