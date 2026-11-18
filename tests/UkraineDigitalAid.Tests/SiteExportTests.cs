using System.Text.Json;
using UkraineDigitalAid.Activities;
using UkraineDigitalAid.Export;
using UkraineDigitalAid.History;
using UkraineDigitalAid.Iati;
using UkraineDigitalAid.Store;

namespace UkraineDigitalAid.Tests;

public class SiteExportTests
{
    private static readonly FetchRun Run = new(
        new DateTime(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc), TimeSpan.Zero, 23855, 1196, 0, 0, 0, 0,
        new Dictionary<string, int>());

    private static readonly Codelists Lists = new(
        new Dictionary<string, string> { ["22040"] = "Information and communication technology (ICT)" },
        new Dictionary<string, string>());

    private static SiteData Export(params Activity[] activities) =>
        SiteExport.Build(activities, Run, Lists, Run.FetchedAt);

    private static Activity Fixture(string name) => ActivityParser.Parse(Tests.Fixture.Record(name));

    [Fact]
    public void Export_has_no_contact_details_or_person_names()
    {
        var data = Export(Fixture("world-bank"), Fixture("fcdo-multi-country"), Fixture("sida-parent"));

        string json = JsonSerializer.Serialize(data, ActivityStore.Json);
        Assert.DoesNotContain("Testenko", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.org", json);
        Assert.DoesNotContain("+380", json);
        Assert.DoesNotContain("person", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Only_selected_activities_are_exported()
    {
        var data = Export(
            Build.Activity(id: "A-1", title: "Government Cloud for Ukraine", sectors: [new SectorShare("22040", 100)]),
            Build.Activity(id: "A-2", title: "Winter clothes for displaced families"));

        Assert.Equal("A-1", data.Activities.Single().Id);
        Assert.Equal(23855, data.Listed);
    }

    [Fact]
    public void Summary_is_given_for_direct_and_for_all()
    {
        var data = Export(
            Build.Activity(id: "A-1", title: "Digital skills"),
            Build.Activity(id: "A-2", title: "Human rights", description: "Includes an information system for complaints."));

        Assert.Equal(1, data.Summary["direct"].Counted);
        Assert.Equal(2, data.Summary["all"].Counted);
    }

    [Fact]
    public void Row_says_why_it_was_selected()
    {
        var row = Export(Build.Activity(title: "Technical assistance", sectors: [new SectorShare("22040", 50), new SectorShare("15110", 50)]))
            .Activities.Single();

        Assert.Equal("sector", row.Basis);
        Assert.Equal(["22040 50%"], row.Why);
        Assert.Equal(50, row.Ict);
        Assert.True(row.Direct);
    }

    [Fact]
    public void Programme_and_its_part_point_to_each_other()
    {
        var data = Export(Fixture("sida-parent"), Fixture("sida-child"));

        Assert.True(data.Activities.Single(a => a.Id == "SE-0-SE-22-68067").HasParts);
        Assert.True(data.Activities.Single(a => a.Id == "SE-0-SE-22-68067").HasDirectParts);
        Assert.Equal("SE-0-SE-22-68067", data.Activities.Single(a => a.Id == "SE-0-SE-22-68067-1").Parent);
    }

    [Fact]
    public void Donor_whose_only_record_is_a_programme_published_in_parts_by_others_keeps_its_name()
    {
        var data = Export(
            Build.Activity(id: "XI-IATI-EC_NEAR-P", title: "Digital programme", publisher: "XI-IATI-EC_NEAR"),
            Build.Activity(id: "XM-DAC-41114-1", title: "Digital output", publisher: "XM-DAC-41114", parentId: "XI-IATI-EC_NEAR-P"));

        Assert.Equal("XI-IATI-EC_NEAR name", data.Donors.Single(d => d.Id == "XI-IATI-EC_NEAR").Name);
    }

    [Fact]
    public void Withheld_title_is_marked_and_keeps_the_donor_text()
    {
        const string redacted = "USAID redacted this field in accordance with the exceptions outlined in the Foreign Aid Transparency and Accountability Act of 2016.";

        var row = Export(Build.Activity(title: redacted, sectors: [new SectorShare("22040", 100)])).Activities.Single();

        Assert.True(row.TitleWithheld);
        Assert.Equal(redacted, row.Title);
    }

    [Fact]
    public void Ordinary_title_is_not_marked_withheld()
    {
        Assert.False(Export(Build.Activity(title: "Digital skills")).Activities.Single().TitleWithheld);
    }

    [Fact]
    public void Programme_with_only_related_parts_counts_among_direct_records()
    {
        var data = Export(
            Build.Activity(id: "SE-0-P", title: "Digital governance programme"),
            Build.Activity(id: "SE-0-P-1", title: "Human rights", description: "Includes an information system.", parentId: "SE-0-P"));

        var programme = data.Activities.Single(a => a.Id == "SE-0-P");
        Assert.True(programme.HasParts);
        Assert.False(programme.HasDirectParts);
        Assert.Equal(1, data.Summary["direct"].Counted);
    }

    [Fact]
    public void Implementers_are_keys_into_the_organisation_list()
    {
        var data = Export(Build.Activity(title: "Digital skills", participants: [Build.Implementer("Beetroot Academy")]));

        string key = data.Activities.Single().Implementers.Single();
        Assert.Equal("Beetroot Academy", data.Organisations[key]);
    }

    [Fact]
    public void Long_description_is_cut_at_a_word()
    {
        string text = string.Join(' ', Enumerable.Repeat("interoperability", 80));

        string shown = Export(Build.Activity(title: "Digital", description: text)).Activities.Single().Description!;

        Assert.True(shown.Length <= SiteExport.DescriptionLength + 1);
        Assert.EndsWith("interoperability…", shown);
    }

    [Fact]
    public void Only_digital_sector_names_are_exported()
    {
        var data = Export(Build.Activity(title: "Cloud", sectors: [new SectorShare("22040", 60), new SectorShare("15110", 40)]));

        Assert.Equal(["22040"], data.Sectors.Keys);
    }

    [Fact]
    public void Energy_theme_is_written_only_for_energy_records()
    {
        var data = Export(
            Build.Activity(id: "DE-1-1", title: "Digital metering for the electricity transmission grid", sectors: [new SectorShare("23630", 100)]),
            Build.Activity(id: "SE-0-2", title: "Digital skills for teachers"));

        var json = JsonSerializer.SerializeToElement(data, ActivityStore.Json).GetProperty("activities");
        var energy = json.EnumerateArray().Single(a => a.GetProperty("id").GetString() == "DE-1-1").GetProperty("energy");
        Assert.False(energy.GetProperty("worthChecking").GetBoolean());
        Assert.False(json.EnumerateArray().Single(a => a.GetProperty("id").GetString() == "SE-0-2").TryGetProperty("energy", out _));
    }

    [Fact]
    public void Changes_compare_the_list_with_the_previous_snapshot()
    {
        var previous = new Snapshot(new DateOnly(2026, 11, 11),
        [
            new SnapshotEntry("A-1", 2, new DateOnly(2026, 11, 15), true),
            new SnapshotEntry("A-9", 2, null, true)
        ]);

        var data = SiteExport.Build(
            [Build.Activity(id: "A-1", title: "Digital skills", end: new DateOnly(2026, 11, 15)), Build.Activity(id: "A-2", title: "Digital registry")],
            Run, Lists, Run.FetchedAt, previous);

        Assert.Equal(new DateOnly(2026, 11, 18), data.Changes!.To);
        Assert.Equal(["A-2"], data.Changes.Added);
        Assert.Equal([new Ended("A-1", EndReason.EndDate)], data.Changes.Ended);
        Assert.Equal("A-9", data.Changes.Gone.Single().Id);
    }

    [Fact]
    public void Without_a_previous_snapshot_there_is_no_change_block()
    {
        Assert.Null(Export(Build.Activity(title: "Digital skills")).Changes);
    }

    [Fact]
    public void Current_snapshot_keeps_status_end_and_kind_of_every_row()
    {
        var data = Export(
            Build.Activity(id: "A-1", title: "Digital skills", status: 2, end: new DateOnly(2027, 3, 31)),
            Build.Activity(id: "A-2", title: "Human rights", description: "Includes an information system.", status: 4));

        var snapshot = SiteExport.CurrentSnapshot(data);

        Assert.Equal(data.Snapshot, snapshot.Date);
        Assert.Equal(
            [new SnapshotEntry("A-1", 2, new DateOnly(2027, 3, 31), true), new SnapshotEntry("A-2", 4, null, false)],
            snapshot.Activities);
    }

    [Fact]
    public void Windows_are_given_for_direct_and_for_all_and_point_to_exported_rows()
    {
        var data = Export(
            Build.Activity(id: "A-1", title: "Digital skills", status: 2, end: new DateOnly(2027, 2, 28)),
            Build.Activity(id: "A-2", title: "Human rights", description: "Includes an information system.", status: 2, end: new DateOnly(2027, 1, 31)));

        Assert.Equal(["A-1"], data.Summary["direct"].Windows);
        Assert.Equal(["A-2", "A-1"], data.Summary["all"].Windows);
        Assert.All(data.Summary["all"].Windows, id => Assert.Contains(data.Activities, a => a.Id == id));
    }
}
