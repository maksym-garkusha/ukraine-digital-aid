using System.Net;
using System.Web;
using UkraineDigitalAid.Iati;
using UkraineDigitalAid.Store;

namespace UkraineDigitalAid.Tests;

public class CollectorTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc);

    // d-portal у мініатюрі: дві активності у списку, одна з них у секторі ІКТ, друга знаходиться словом у назві
    private static HttpResponseMessage Portal(Uri uri, bool xsonFails = false)
    {
        var q = HttpUtility.ParseQueryString(uri.Query);
        if (q["from"] == "xson")
        {
            if (xsonFails)
                return FakeHandler.Json("", HttpStatusCode.BadRequest);
            string name = q["aid"] == "SE-0-SE-22-68067-1" ? "sida-child" : "german-ffo";
            return FakeHandler.Json($"{{\"xson\":[{{\"/iati-activities/iati-activity\":[{File.ReadAllText(Fixture.Path(name))}]}}]}}");
        }
        if (q["offset"] != "0")
            return FakeHandler.Json("{\"rows\":[]}");
        if (q["select"] == ListRow.Columns)
            return FakeHandler.Json("""
                {"rows":[
                  {"aid":"SE-0-SE-22-68067-1","title":"Government Cloud for Ukraine","status_code":4,"commitment":250000,"spend":240000,"country_percent":100},
                  {"aid":"XM-DAC-5-7-6612999","title":"Digital media marathon for NGO workers in Ukraine","status_code":4,"country_percent":100},
                  {"aid":"XM-DAC-5-7-1","title":"Winter clothes","status_code":4,"country_percent":100}]}
                """);
        if (q["sector_code"] == "22040")
            return FakeHandler.Json("{\"rows\":[{\"aid\":\"SE-0-SE-22-68067-1\"}]}");
        return FakeHandler.Json("{\"rows\":[]}");
    }

    private static async Task<(Collected Result, FakeHandler Handler)> Collect(
        IReadOnlyDictionary<string, CachedActivity>? cache = null, bool xsonFails = false)
    {
        var handler = new FakeHandler((uri, _) => Portal(uri, xsonFails));
        var collector = new Collector(new DPortalClient(handler.Client(), TimeSpan.Zero, TimeSpan.Zero), TextWriter.Null);
        var result = await collector.RunAsync(cache ?? new Dictionary<string, CachedActivity>(), Now, CancellationToken.None);
        return (result, handler);
    }

    private static int XsonRequests(FakeHandler handler) =>
        handler.Requests.Count(r => HttpUtility.ParseQueryString(r.Query)["from"] == "xson");

    [Fact]
    public async Task Candidates_come_from_sectors_and_titles()
    {
        var (result, _) = await Collect();

        Assert.Equal(3, result.Run.Listed);
        Assert.Equal(2, result.Run.Candidates);
        Assert.Equal(1, result.Run.Sources["sector 22040"]);
        Assert.Equal(1, result.Run.Sources["title"]);
        Assert.Equal(["SE-0-SE-22-68067-1", "XM-DAC-5-7-6612999"], result.Entries.Select(e => e.Activity.Id).Order());
    }

    [Fact]
    public async Task Sums_from_the_list_are_attached_to_full_records()
    {
        var (result, _) = await Collect();

        var cloud = result.Entries.Single(e => e.Activity.Id == "SE-0-SE-22-68067-1").Activity;
        Assert.Equal(250000m, cloud.Commitment);
    }

    [Fact]
    public async Task Unchanged_fresh_record_is_taken_from_cache()
    {
        var (first, _) = await Collect();
        var cache = first.Entries.ToDictionary(e => e.Activity.Id);

        var (second, handler) = await Collect(cache);

        Assert.Equal(0, XsonRequests(handler));
        Assert.Equal(2, second.Run.FromCache);
    }

    [Fact]
    public async Task Changed_record_is_downloaded_again()
    {
        var (first, _) = await Collect();
        var cache = first.Entries.ToDictionary(e => e.Activity.Id, e => e with { Fingerprint = "old" });

        var (second, handler) = await Collect(cache);

        Assert.Equal(2, XsonRequests(handler));
        Assert.Equal(2, second.Run.Downloaded);
    }

    [Fact]
    public async Task Old_record_is_downloaded_again()
    {
        var (first, _) = await Collect();
        var cache = first.Entries.ToDictionary(e => e.Activity.Id, e => e with { FetchedAt = Now.AddDays(-40) });

        var (_, handler) = await Collect(cache);

        Assert.Equal(2, XsonRequests(handler));
    }

    [Fact]
    public async Task Failed_download_keeps_the_cached_version()
    {
        var (first, _) = await Collect();
        var cache = first.Entries.ToDictionary(e => e.Activity.Id, e => e with { Fingerprint = "old" });

        var (second, _) = await Collect(cache, xsonFails: true);

        Assert.Equal(2, second.Run.Failed);
        Assert.Equal(2, second.Entries.Count);
    }

    [Fact]
    public void Collection_much_smaller_than_last_time_is_a_problem()
    {
        var previous = Summary(listed: 23857, candidates: 1300);

        Assert.Null(Collector.Problem(Summary(23900, 1310), previous));
        Assert.NotNull(Collector.Problem(Summary(9000, 1310), previous));
        Assert.NotNull(Collector.Problem(Summary(23900, 500), previous));
    }

    [Fact]
    public void Many_failed_downloads_are_a_problem()
    {
        Assert.NotNull(Collector.Problem(Summary(23857, 1300, failed: 200), null));
    }

    private static FetchRun Summary(int listed, int candidates, int failed = 0) =>
        new(Now, TimeSpan.Zero, listed, candidates, candidates - failed, 0, 0, failed, new Dictionary<string, int>());
}
