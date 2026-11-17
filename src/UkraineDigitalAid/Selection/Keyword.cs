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

public record Keyword(string Text, KeywordKind Kind)
{
    public static Keyword Word(string text) => new(text, KeywordKind.Word);

    public static Keyword Stem(string text) => new(text, KeywordKind.Stem);

    public static Keyword CaseSensitive(string text) => new(text, KeywordKind.CaseSensitive);

    /// <summary>
    /// Запит для text_search на d-portal. Там повнотекстовий пошук PostgreSQL з
    /// конфігурацією simple: цілі слова без основ і з регістром, зведеним до малих літер.
    /// Тому основи й «IT» (він збігся б з англійським it) так не шукаються; ці слова
    /// ловить перегляд назв усіх активностей.
    /// </summary>
    public string? SearchTerm => Kind == KeywordKind.Word ? Text : null;

    public bool IsIn(string text)
    {
        string haystack = Normalize(text);
        var comparison = Kind == KeywordKind.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        for (int at = haystack.IndexOf(Text, comparison); at >= 0; at = haystack.IndexOf(Text, at + 1, comparison))
        {
            if (at > 0 && char.IsLetterOrDigit(haystack[at - 1]))
                continue;
            if (Kind == KeywordKind.Stem)
                return true;
            int end = at + Text.Length;
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
