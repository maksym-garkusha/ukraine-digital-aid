using System.Text.Json;

namespace UkraineDigitalAid.Iati;

/// <summary>
/// Довідники IATI: назви секторів DAC і статусів активності англійською.
/// Під час fetch вони завантажуються з сайту IATI і лежать у кеші поруч з активностями.
/// </summary>
public record Codelists(IReadOnlyDictionary<string, string> Sectors, IReadOnlyDictionary<string, string> Statuses)
{
    public static readonly string[] Names = ["Sector", "ActivityStatus"];

    public static string Url(string name) =>
        $"https://cdn.iatistandard.org/prod-iati-website/reference_downloads/203/codelists/downloads/clv3/json/en/{name}.json";

    /// <summary>Код → назва. Формат: {"data":[{"code":"22040","name":"...","status":"active"}]}.</summary>
    public static Dictionary<string, string> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new Dictionary<string, string>();
        foreach (var item in document.RootElement.GetProperty("data").EnumerateArray())
            if (item.TryGetProperty("code", out var code) && item.TryGetProperty("name", out var name))
                result[code.ToString()] = name.GetString() ?? "";
        return result;
    }

    public static async Task DownloadAsync(HttpClient http, string directory, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        foreach (string name in Names)
        {
            string json = await http.GetStringAsync(Url(name), ct);
            Parse(json); // не зберігаємо те, що не розбирається
            await File.WriteAllTextAsync(Path.Combine(directory, name + ".json"), json, ct);
        }
    }

    public static Codelists Read(string directory)
    {
        Dictionary<string, string> Load(string name)
        {
            string path = Path.Combine(directory, name + ".json");
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : [];
        }
        return new Codelists(Load("Sector"), Load("ActivityStatus"));
    }
}
