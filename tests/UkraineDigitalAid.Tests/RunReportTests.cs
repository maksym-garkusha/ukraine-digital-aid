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
    public void Report_lists_activities_ending_within_a_year()
    {
        string report = RunReport.Build([
            Build.Activity(id: "A-1", title: "Digital skills", status: 2, end: new DateOnly(2027, 3, 31))
        ], Run, Lists);

        Assert.Contains("| 31.03.2027 |", report);
        Assert.Contains("view=act&aid=A-1", report);
    }
}
