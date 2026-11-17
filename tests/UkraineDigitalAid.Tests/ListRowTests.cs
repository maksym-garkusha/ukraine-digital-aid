using System.Text.Json;
using UkraineDigitalAid.Iati;

namespace UkraineDigitalAid.Tests;

public class ListRowTests
{
    // справжній рядок з /q?from=act,country&country_code=UA
    private const string Row = """
        {"aid":"NL-KVK-41158230-SHONUA0003","title":"MUL074N SHO actie voor Oekraine - Giro555 - Ukraine",
         "reporting":"Care Nederland","reporting_ref":"NL-KVK-41158230","status_code":4,
         "day_start":19083,"day_end":19265,"commitment":2260790.2,"spend":2284348.2,"country_percent":100}
        """;

    [Fact]
    public void Days_since_epoch_become_dates()
    {
        var row = JsonSerializer.Deserialize<ListRow>(Row)!;

        Assert.Equal(new DateOnly(2022, 4, 1), row.Start);
        Assert.Equal(new DateOnly(2022, 9, 30), row.End);
    }

    [Fact]
    public void Missing_end_day_gives_no_date()
    {
        var row = JsonSerializer.Deserialize<ListRow>("""{"aid":"X-1","day_start":19710,"day_end":null}""")!;

        Assert.Null(row.End);
    }

    [Fact]
    public void Fingerprint_changes_when_amount_changes()
    {
        var row = JsonSerializer.Deserialize<ListRow>(Row)!;

        Assert.NotEqual(row.Fingerprint(), (row with { Spend = 2300000m }).Fingerprint());
    }

    [Theory]
    [InlineData("""{"aid":"X-1","commitment":"n/a"}""")]
    [InlineData("""{"title":"No identifier"}""")]
    [InlineData("""{"aid":"","title":"Empty identifier"}""")]
    public void Unreadable_row_is_skipped_not_thrown(string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Null(ListRow.TryRead(document.RootElement));
    }

    [Fact]
    public void Readable_row_is_read()
    {
        using var document = JsonDocument.Parse(Row);

        Assert.Equal("NL-KVK-41158230-SHONUA0003", ListRow.TryRead(document.RootElement)!.Aid);
    }
}
