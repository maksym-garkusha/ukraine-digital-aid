using System.Net;
using System.Web;
using UkraineDigitalAid.Iati;

namespace UkraineDigitalAid.Tests;

public class DPortalClientTests
{
    private static DPortalClient Client(FakeHandler handler) => new(handler.Client(), TimeSpan.Zero, TimeSpan.Zero);

    private static string Rows(params string[] aids) =>
        "{\"rows\":[" + string.Join(',', aids.Select(a => $"{{\"aid\":\"{a}\"}}")) + "]}";

    private static async Task<string> FirstAid(FakeHandler handler)
    {
        using var result = await Client(handler).QueryAsync([("from", "act")], CancellationToken.None);
        return result.RootElement.GetProperty("rows")[0].GetProperty("aid").GetString()!;
    }

    private static int Offset(Uri uri) => int.Parse(HttpUtility.ParseQueryString(uri.Query)["offset"]!);

    [Fact]
    public async Task List_reads_pages_until_a_short_page()
    {
        string[] all = ["A-1", "A-2", "A-3", "A-4", "A-5"];
        var handler = new FakeHandler((uri, _) => FakeHandler.Json(Rows(all.Skip(Offset(uri)).Take(2).ToArray())));

        var aids = new List<string>();
        await foreach (var row in Client(handler).ListAsync([("from", "act")], "aid", 2, CancellationToken.None))
            aids.Add(row.GetProperty("aid").GetString()!);

        Assert.Equal(all, aids);
        Assert.Equal([0, 2, 4], handler.Requests.Select(Offset));
    }

    [Fact]
    public async Task List_asks_for_stable_order()
    {
        var handler = new FakeHandler((_, _) => FakeHandler.Json(Rows()));

        await foreach (var _ in Client(handler).ListAsync([("from", "act")], "aid", 10, CancellationToken.None)) { }

        Assert.Equal("aid", HttpUtility.ParseQueryString(handler.Requests[0].Query)["orderby"]);
    }

    [Fact]
    public async Task Server_error_is_retried()
    {
        var handler = new FakeHandler((_, n) => n < 3
            ? FakeHandler.Json("", HttpStatusCode.ServiceUnavailable)
            : FakeHandler.Json(Rows("A-1")));

        Assert.Equal("A-1", await FirstAid(handler));
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Too_many_requests_is_retried()
    {
        var handler = new FakeHandler((_, n) => n == 1
            ? FakeHandler.Json("", HttpStatusCode.TooManyRequests)
            : FakeHandler.Json(Rows("A-1")));

        Assert.Equal("A-1", await FirstAid(handler));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Persistent_server_error_gives_up_after_four_attempts()
    {
        var handler = new FakeHandler((_, _) => FakeHandler.Json("", HttpStatusCode.BadGateway));

        await Assert.ThrowsAsync<HttpRequestException>(() => FirstAid(handler));
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task Client_error_is_not_retried()
    {
        var handler = new FakeHandler((_, _) => FakeHandler.Json("", HttpStatusCode.BadRequest));

        await Assert.ThrowsAsync<HttpRequestException>(() => FirstAid(handler));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Broken_connection_is_retried()
    {
        int calls = 0;
        var handler = new FakeHandler((_, _) => ++calls == 1
            ? throw new HttpRequestException("connection reset")
            : FakeHandler.Json(Rows("A-5")));

        Assert.Equal("A-5", await FirstAid(handler));
    }

    [Fact]
    public async Task Unknown_activity_gives_null()
    {
        var handler = new FakeHandler((_, _) => FakeHandler.Json("{\"xson\":[],\"count\":0}"));

        Assert.Null(await Client(handler).GetActivityAsync("NOPE-1", CancellationToken.None));
    }

    [Fact]
    public async Task Activity_record_is_taken_from_the_activity_root()
    {
        var handler = new FakeHandler((_, _) => FakeHandler.Json(
            "{\"xson\":[{\"/iati-activities/iati-activity\":[{\"/iati-identifier\":\"SE-0-SE-6-1\"}]}]}"));

        var record = await Client(handler).GetActivityAsync("SE-0-SE-6-1", CancellationToken.None);

        Assert.Equal("SE-0-SE-6-1", record!.Value.GetProperty("/iati-identifier").GetString());
        Assert.Equal("/iati-activities/iati-activity", HttpUtility.ParseQueryString(handler.Requests[0].Query)["root"]);
    }

    [Fact]
    public void Url_escapes_values_and_asks_for_json()
    {
        string url = DPortalClient.BuildUrl([("text_search", "open data"), ("aid", "XI-IATI-EC_NEAR-2019/1")]);

        Assert.Equal("q?text_search=open%20data&aid=XI-IATI-EC_NEAR-2019%2F1&form=json", url);
    }

    [Fact]
    public async Task Body_that_never_ends_fails_by_timeout()
    {
        var handler = new FakeHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new EndlessStream()) });
        using var http = handler.Client();
        http.Timeout = TimeSpan.FromMilliseconds(100);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => new DPortalClient(http, TimeSpan.Zero, TimeSpan.Zero).QueryAsync([("from", "act")], CancellationToken.None));
        Assert.Equal(4, handler.Requests.Count);
    }

    // сервер надіслав заголовки, а тіло не віддає
    private sealed class EndlessStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }
    }
}
