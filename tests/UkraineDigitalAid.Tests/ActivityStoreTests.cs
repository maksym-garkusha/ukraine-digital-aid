using UkraineDigitalAid.Activities;
using UkraineDigitalAid.Store;

namespace UkraineDigitalAid.Tests;

public sealed class ActivityStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "uda-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static FetchRun Run(int listed = 10) =>
        new(new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(5), listed, 3, 3, 0, 0, 0,
            new Dictionary<string, int> { ["title"] = 3 });

    [Fact]
    public async Task Saved_activities_load_back_unchanged()
    {
        var activity = ActivityParser.Parse(Fixture.Record("sida-child"));
        var entry = new CachedActivity("fp", new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc), activity);
        var store = new ActivityStore(_directory);

        await store.SaveAsync([entry], Run(), CancellationToken.None);
        var loaded = store.Load()[activity.Id];

        Assert.Equal(activity.Title, loaded.Activity.Title);
        Assert.Equal(activity.Sectors, loaded.Activity.Sectors);
        Assert.Equal(activity.Participants, loaded.Activity.Participants);
        Assert.Equal(activity.End, loaded.Activity.End);
        Assert.Equal("fp", loaded.Fingerprint);
    }

    [Fact]
    public async Task Cache_is_one_json_line_per_activity()
    {
        var store = new ActivityStore(_directory);
        var entries = new[] { "sida-child", "sida-parent", "undp" }
            .Select(f => new CachedActivity("", DateTime.UtcNow, ActivityParser.Parse(Fixture.Record(f))));

        await store.SaveAsync(entries, Run(), CancellationToken.None);

        var lines = File.ReadAllLines(Directory.GetFiles(_directory, "*.jsonl").Single());
        Assert.Equal(3, lines.Length);
        Assert.All(lines, line => Assert.StartsWith("{", line));
    }

    [Fact]
    public async Task Last_fetch_summary_is_kept()
    {
        var store = new ActivityStore(_directory);

        await store.SaveAsync([], Run(listed: 23857), CancellationToken.None);

        Assert.Equal(23857, store.LoadRun()!.Listed);
    }

    [Fact]
    public void Empty_cache_loads_as_nothing()
    {
        var store = new ActivityStore(_directory);

        Assert.Empty(store.Load());
        Assert.Null(store.LoadRun());
    }

    [Fact]
    public async Task Failed_write_keeps_the_previous_file()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "data.json");
        await File.WriteAllTextAsync(path, "old");

        await Assert.ThrowsAsync<IOException>(() => AtomicFile.WriteAsync(path, async file =>
        {
            await file.WriteAsync("new, but cut"u8.ToArray());
            throw new IOException("disk full");
        }));

        Assert.Equal("old", await File.ReadAllTextAsync(path));
        Assert.Equal([path], Directory.GetFiles(_directory));
    }
}
