using UkraineDigitalAid.Activities;
using UkraineDigitalAid.Iati;
using UkraineDigitalAid.Report;
using UkraineDigitalAid.Store;

namespace UkraineDigitalAid.Tests;

public class RunReportTests
{
    private static readonly FetchRun Run = new(
        new DateTime(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(12), 23855, 1196, 1196, 0, 0, 0,
        new Dictionary<string, int> { ["sector 22040"] = 83 });

    private static readonly Codelists Lists = new(new Dictionary<string, string>(),
        new Dictionary<string, string> { ["2"] = "Implementation" });

    [Fact]
    public void Report_counts_direct_and_tangential_records()
    {
        string report = RunReport.Build([
            Build.Activity(id: "A-1", title: "Digital skills"),
            Build.Activity(id: "A-2", title: "Human rights", description: "Includes an information system.")
        ], Run, Lists);

        Assert.Contains("Відібрано 2 записів: 1 прямих і 1 дотичних.", report);
        Assert.Contains("| Implementation | 1 |", report);
    }

    [Fact]
    public void Report_counts_military_records_apart()
    {
        string report = RunReport.Build([
            Build.Activity(id: "US-GOV-7-1", title: "Ministry of Defense - Cyber Support", publisher: "US-GOV-7"),
            Build.Activity(id: "A-2", title: "Digital skills")
        ], Run, Lists);

        Assert.Contains("Відібрано 2 записів: 1 прямих і 1 дотичних.", report);
        Assert.Contains("завжди дотичні: 1, з них зі словом у назві 1.", report);
    }

    [Fact]
    public void Report_lists_activities_ending_within_a_year()
    {
        string report = RunReport.Build([
            Build.Activity(id: "A-1", title: "Digital skills", status: 2, end: new DateOnly(2027, 3, 31))
        ], Run, Lists);

        Assert.Contains("| 31.03.2027 |", report);
        Assert.Contains("view=act&aid=A-1", report);
    }

    [Fact]
    public void Withheld_title_is_replaced_in_the_ending_soon_list()
    {
        string report = RunReport.Build([
            Build.Activity(id: "US-GOV-11-1", status: 2, end: new DateOnly(2027, 3, 31), sectors: [new SectorShare("22040", 50)],
                title: "Information has been Redacted in accordance with the two principled exceptions of the Foreign Aid Transparency and Accountability Act (FATAA) of 2016")
        ], Run, Lists);

        Assert.Contains("[назву приховано донором](https://d-portal.org/ctrack.html#view=act&aid=US-GOV-11-1)", report);
        Assert.DoesNotContain("in accordance with", report);
    }
}
