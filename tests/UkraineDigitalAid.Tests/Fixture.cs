using System.Text.Json;

namespace UkraineDigitalAid.Tests;

/// <summary>
/// Записи з папки Fixtures. Це справжні активності з d-portal, скорочені: прибрано
/// результати, бюджети, документи і більшість транзакцій. Контактні дані й ім'я
/// підрядника-фізособи замінено вигаданими.
/// </summary>
public static class Fixture
{
    public static string Path(string name) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".json");

    public static JsonElement Record(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path(name)));
        return document.RootElement.Clone();
    }

    public static JsonElement Json(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
}
