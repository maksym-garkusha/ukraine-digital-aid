namespace UkraineDigitalAid.Selection;

public enum KeywordKind
{
    /// <summary>Слово чи фраза без урахування регістру, за межами слова; допускається «s» у кінці.</summary>
    Word,
    /// <summary>Початок слова, для українських основ: «цифров» знаходить «цифрової».</summary>
    Stem,
    /// <summary>Окреме слово з точним регістром: «IT» так, «it» і «ITALY» ні.</summary>
    CaseSensitive
}

/// <param name="Plural">Множина, яку не дає просте «s» у кінці, або яку треба окремо шукати на d-portal.</param>
public record Keyword(string Text, KeywordKind Kind, string? Plural = null)
{
    public static Keyword Word(string text, string? plural = null) => new(text, KeywordKind.Word, plural);

    public static Keyword Stem(string text) => new(text, KeywordKind.Stem);

    public static Keyword CaseSensitive(string text) => new(text, KeywordKind.CaseSensitive);

    /// <summary>
    /// Запити для text_search на d-portal. Там повнотекстовий пошук PostgreSQL з
    /// конфігурацією simple: цілі слова без основ і з регістром, зведеним до малих літер.
    /// Тому множину треба шукати окремо, а основи й «IT» (він збігся б з англійським it)
    /// так не шукаються зовсім; ці слова ловить перегляд назв усіх активностей.
    /// </summary>
    public IEnumerable<string> SearchTerms =>
        Kind != KeywordKind.Word ? [] : Plural is null ? [Text] : [Text, Plural];

    public bool IsIn(string text)
    {
        string haystack = Normalize(text);
        return Occurs(haystack, Text) || (Plural is not null && Occurs(haystack, Plural));
    }

    private bool Occurs(string haystack, string word)
    {
        var comparison = Kind == KeywordKind.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        for (int at = haystack.IndexOf(word, comparison); at >= 0; at = haystack.IndexOf(word, at + 1, comparison))
        {
            if (at > 0 && char.IsLetterOrDigit(haystack[at - 1]))
                continue;
            if (Kind == KeywordKind.Stem)
                return true;
            int end = at + word.Length;
            if (Kind == KeywordKind.Word && end < haystack.Length && haystack[end] is 's' or 'S')
                end++;
            if (end >= haystack.Length || !char.IsLetterOrDigit(haystack[end]))
                return true;
        }
        return false;
    }

    // нерозривні пробіли й дефіси, перенесення рядків: фраза має знаходитися попри них
    private static string Normalize(string text)
    {
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i]))
                chars[i] = ' ';
            else if (chars[i] is '‐' or '‑')
                chars[i] = '-';
        }
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
