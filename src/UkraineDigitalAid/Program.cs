using System.Text.Json;
using UkraineDigitalAid.Export;
using UkraineDigitalAid.History;
using UkraineDigitalAid.Iati;
using UkraineDigitalAid.Report;
using UkraineDigitalAid.Store;

// dotnet run -- fetch                 забрати активності з d-portal у кеш
// dotnet run -- export <тека>         зібрати data.json, snapshot.json і стрічки змін для сторінки
// dotnet run -- report <файл.md>      зібрати звіт за даними з кешу
// Кеш лежить у теці cache поруч із місцем запуску, інше місце: --cache <тека>
// export --previous <snapshot.json>   опублікований зріз на випадок, якщо в кеші зрізів немає
string cacheDir = OptionValue(ref args, "--cache") ?? "cache";
string? previous = OptionValue(ref args, "--previous");
var store = new ActivityStore(cacheDir);
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };

switch (args)
{
    case ["fetch"]:
        return await Fetch(store, cancel.Token);
    case ["export", var directory]:
        return await Export(store, directory, previous, cancel.Token);
    case ["report", var path]:
        return await Report(store, path, cancel.Token);
    default:
        Console.Error.WriteLine("Команди: fetch | export <тека> [--previous <snapshot.json>] | report <файл.md> [--cache <тека>]");
        return 2;
}

static async Task<int> Fetch(ActivityStore store, CancellationToken ct)
{
    // найбільша справжня відповідь, сторінка списку на 2000 рядків, важить близько мегабайта
    using var http = new HttpClient
    {
        BaseAddress = new Uri("https://d-portal.org/"),
        Timeout = TimeSpan.FromMinutes(3),
        MaxResponseContentBufferSize = 32 * 1024 * 1024
    };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("ukraine-digital-aid/1.0 (+https://github.com/maksym-garkusha/ukraine-digital-aid)");

    bool haveCodelists = Codelists.Names.All(name => File.Exists(Path.Combine(store.CodelistsDirectory, name + ".json")));
    try
    {
        await Codelists.DownloadAsync(http, store.CodelistsDirectory, ct);
    }
    catch (Exception e) when (haveCodelists && IsNetworkFailure(e, ct))
    {
        Console.WriteLine($"Довідники IATI не оновлено, лишаються попередні: {e.Message}");
    }

    var collector = new Collector(DPortalClient.Create(http), Console.Out);
    Collected collected;
    try
    {
        collected = await collector.RunAsync(store.Load(), DateTime.UtcNow, ct);
    }
    catch (Exception e) when (IsNetworkFailure(e, ct) || e is JsonException)
    {
        Console.Error.WriteLine($"d-portal недоступний або відповів не тим: {e.Message}. Кеш не змінено.");
        return 1;
    }

    var run = collected.Run;
    Console.WriteLine($"Забрано {run.Downloaded}, з кешу {run.FromCache}, не знайдено {run.NotFound}, з помилкою {run.Failed}. Час: {run.Duration:hh\\:mm\\:ss}");
    if (Collector.Problem(run, store.LoadRun()) is { } problem)
    {
        Console.Error.WriteLine($"Збір не схожий на справжній: {problem}. Кеш не змінено.");
        return 1;
    }
    await store.SaveAsync(collected.Entries, run, ct);
    return 0;
}

static async Task<int> Export(ActivityStore store, string directory, string? previous, CancellationToken ct)
{
    if (store.LoadRun() is not { } run)
        return NoCache(store);
    var today = DateOnly.FromDateTime(run.FetchedAt);
    var snapshots = new SnapshotStore(store.SnapshotsDirectory);
    if (previous is not null)
        await ImportSnapshot(snapshots, previous, today, ct);

    var baseline = snapshots.Baseline(today);
    var data = SiteExport.Build(store.Load().Values.Select(e => e.Activity), run, Codelists.Read(store.CodelistsDirectory),
        DateTime.UtcNow, baseline);
    long size = await SiteExport.WriteAsync(data, directory, ct);
    await ChangesFeed.WriteAsync(data, directory, ct);
    await snapshots.SaveAsync(SiteExport.CurrentSnapshot(data), ct);

    Console.WriteLine($"{Path.Combine(directory, SiteExport.FileName)}: {data.Activities.Count} активностей, {size / 1024} КБ");
    Console.WriteLine(data.Changes is { } c
        ? $"Зміни з {c.From:dd.MM.yyyy}: нових {c.Added.Count}, завершилися {c.Ended.Count}, зникли {c.Gone.Count}"
        : "Попереднього зрізу немає, блок змін буде порожній");
    return 0;
}

// опублікований зріз потрібен лише як запасний; зіпсований файл не має зупиняти публікацію
static async Task ImportSnapshot(SnapshotStore snapshots, string path, DateOnly today, CancellationToken ct)
{
    try
    {
        if (await snapshots.ImportAsync(path, today, ct) is { } imported)
            Console.WriteLine($"Узято опублікований зріз від {imported.Date:dd.MM.yyyy}");
    }
    catch (JsonException e)
    {
        Console.WriteLine($"Опублікований зріз не розібрано, він не враховується: {e.Message}");
    }
}

static async Task<int> Report(ActivityStore store, string path, CancellationToken ct)
{
    if (store.LoadRun() is not { } run)
        return NoCache(store);
    string report = RunReport.Build(store.Load().Values.Select(e => e.Activity), run, Codelists.Read(store.CodelistsDirectory));
    if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
        Directory.CreateDirectory(directory);
    await AtomicFile.WriteTextAsync(path, report, ct);
    Console.WriteLine($"Звіт записано в {path}");
    return 0;
}

// тайм-аут HttpClient приходить як TaskCanceledException без скасування з нашого боку
static bool IsNetworkFailure(Exception e, CancellationToken ct) =>
    e is HttpRequestException || (e is TaskCanceledException && !ct.IsCancellationRequested);

static int NoCache(ActivityStore store)
{
    Console.Error.WriteLine($"У {store.Directory} немає зібраних даних. Спершу виконайте fetch.");
    return 1;
}

static string? OptionValue(ref string[] args, string name)
{
    int at = Array.IndexOf(args, name);
    if (at < 0 || at + 1 >= args.Length)
        return null;
    string value = args[at + 1];
    args = [.. args[..at], .. args[(at + 2)..]];
    return value;
}
