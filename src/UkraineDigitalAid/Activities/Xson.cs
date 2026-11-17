using System.Globalization;
using System.Text.Json;

namespace UkraineDigitalAid.Activities;

/// <summary>
/// Доступ до запису у форматі xson. Ключі там це шляхи XML, і d-portal склеює
/// їх по-різному: та сама назва може лежати під "/title/narrative" або під
/// "/title" → "/narrative", атрибут під "/reporting-org@ref" або "/reporting-org" → "@ref".
/// Тому шлях шукається за найдовшим ключем, що збігається з його початком.
/// </summary>
public static class Xson
{
    public static IEnumerable<JsonElement> Values(JsonElement node, string path)
    {
        if (path.Length == 0)
        {
            foreach (var item in Expand(node))
                yield return item;
            yield break;
        }
        if (node.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (int end in Boundaries(path))
        {
            if (!node.TryGetProperty(path[..end], out var child))
                continue;
            foreach (var item in Expand(child))
                foreach (var value in Values(item, path[end..]))
                    yield return value;
            yield break;
        }
    }

    public static string? String(JsonElement node, string path)
    {
        foreach (var value in Values(node, path))
            if (AsString(value) is { Length: > 0 } text)
                return text;
        return null;
    }

    public static double? Number(JsonElement node, string path)
    {
        foreach (var value in Values(node, path))
        {
            if (value.ValueKind == JsonValueKind.Number)
                return value.GetDouble();
            if (value.ValueKind == JsonValueKind.String
                && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                return parsed;
        }
        return null;
    }

    /// <summary>
    /// Тексти narrative з мовами. Мову без @xml:lang беремо з активності. Однакові
    /// тексти й повтори тієї самої мови відкидаються: деякі донори дублюють narrative.
    /// </summary>
    public static List<Narrative> Narratives(JsonElement node, string path, string defaultLang)
    {
        var result = new List<Narrative>();
        foreach (var item in Values(node, path))
        {
            string? text = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("", out var t)
                ? AsString(t) : AsString(item);
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
                continue;
            string lang = Lang(item) ?? defaultLang;
            if (result.Any(n => n.Lang == lang || n.Text == text))
                continue;
            result.Add(new Narrative(lang, text));
        }
        return result;
    }

    public static string? Lang(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty("@xml:lang", out var lang)
            && AsString(lang) is { Length: > 0 } code
            ? code.Trim().ToLowerInvariant()
            : null;

    private static string? AsString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        _ => null
    };

    private static IEnumerable<JsonElement> Expand(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [value];

    // кінці можливих ключів, від найдовшого: "/a/b@c", "/a/b", "/a"
    private static IEnumerable<int> Boundaries(string path)
    {
        yield return path.Length;
        for (int i = path.Length - 1; i > 0; i--)
            if (path[i] is '/' or '@')
                yield return i;
    }
}
