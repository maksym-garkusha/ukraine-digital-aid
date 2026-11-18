using System.Text.Json;
using UkraineDigitalAid.History;

namespace UkraineDigitalAid.Tests;

public sealed class SnapshotStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "uda-" + Guid.NewGuid());
    private static readonly DateOnly Today = new(2026, 11, 23);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static Snapshot Snapshot(DateOnly date) => new(date, [new SnapshotEntry("SE-0-1", 2, new DateOnly(2027, 6, 30), true)]);

    private async Task<SnapshotStore> StoreWith(params DateOnly[] dates)
    {
        var store = new SnapshotStore(Path.Combine(_directory, "snapshots"));
        foreach (var date in dates)
            await store.SaveAsync(Snapshot(date), CancellationToken.None);
        return store;
    }

    // так workflow кладе поруч опублікований snapshot.json
    private async Task<string> Published(Snapshot snapshot)
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "published.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(snapshot, Store.ActivityStore.Json));
        return path;
    }

    [Fact]
    public async Task Saved_snapshot_loads_back_unchanged()
    {
        var store = await StoreWith(Today);

        var loaded = store.Load(Today);

        Assert.Equal(Today, loaded.Date);
        Assert.Equal(Snapshot(Today).Activities, loaded.Activities);
    }

    [Fact]
    public async Task Baseline_is_the_latest_snapshot_at_least_a_week_old()
    {
        // 17.11 щотижневий збір з запізненням, 20.11 перезбірка після пушу
        var store = await StoreWith(new(2026, 11, 9), new(2026, 11, 17), new(2026, 11, 20), Today);

        Assert.Equal(new DateOnly(2026, 11, 17), store.Baseline(Today)!.Date);
    }

    [Fact]
    public async Task Without_a_week_old_snapshot_the_oldest_earlier_one_is_used()
    {
        var store = await StoreWith(new(2026, 11, 19), new(2026, 11, 21));

        Assert.Equal(new DateOnly(2026, 11, 19), store.Baseline(Today)!.Date);
    }

    [Fact]
    public async Task Snapshot_of_the_same_day_is_not_a_baseline()
    {
        var store = await StoreWith(Today);

        Assert.Null(store.Baseline(Today));
    }

    [Fact]
    public async Task Snapshots_older_than_the_keep_period_are_removed()
    {
        var store = await StoreWith(Today.AddDays(-SnapshotStore.KeepDays - 1), Today.AddDays(-SnapshotStore.KeepDays), Today);

        Assert.Equal([Today.AddDays(-SnapshotStore.KeepDays), Today], store.Dates());
    }

    [Fact]
    public async Task Published_snapshot_fills_in_when_the_cache_has_none()
    {
        var store = await StoreWith();
        string published = await Published(Snapshot(new(2026, 11, 16)));

        var imported = await store.ImportAsync(published, Today, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 11, 16), imported!.Date);
        Assert.Equal(new DateOnly(2026, 11, 16), store.Baseline(Today)!.Date);
    }

    [Theory]
    [InlineData(2026, 11, 23)] // того самого дня
    [InlineData(2099, 1, 1)]   // з майбутнього, інакше він видалив би всі справжні зрізи
    [InlineData(2026, 9, 1)]   // старший за строк зберігання
    public async Task Published_snapshot_with_a_useless_date_is_ignored(int year, int month, int day)
    {
        var store = await StoreWith(new DateOnly(2026, 11, 16));
        string published = await Published(Snapshot(new(year, month, day)));

        Assert.Null(await store.ImportAsync(published, Today, CancellationToken.None));
        Assert.Equal([new DateOnly(2026, 11, 16)], store.Dates());
    }

    [Fact]
    public async Task Own_snapshot_wins_over_the_published_one_for_the_same_day()
    {
        var store = await StoreWith(new DateOnly(2026, 11, 16));
        string published = await Published(new Snapshot(new(2026, 11, 16), []));

        Assert.Null(await store.ImportAsync(published, Today, CancellationToken.None));
        Assert.Single(store.Load(new(2026, 11, 16)).Activities);
    }

    [Theory]
    [InlineData("{\"generatedAt\":\"2026-11-18T00:50:28Z\",\"activities\":[]}")]
    [InlineData("{\"date\":\"2026-11-16\",\"activities\":[{\"status\":2}]}")]
    [InlineData("[]")]
    public void Other_json_is_not_taken_for_a_snapshot(string json)
    {
        Assert.Throws<JsonException>(() => SnapshotStore.Parse(json));
    }
}
