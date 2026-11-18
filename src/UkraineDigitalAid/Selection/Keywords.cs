namespace UkraineDigitalAid.Selection;

/// <summary>
/// Перелік слів для відбору «цифрових» активностей. З нього беруть слова пошук кандидатів
/// на d-portal, відбір у DigitalRules і перелік у розділі методики на сторінці (через data.json).
/// Перелік у README.md і README.en.md пишеться вручну: після правки тут оновити і його.
/// </summary>
public static class Keywords
{
    public static readonly IReadOnlyList<Keyword> All =
    [
        Keyword.Word("digital"),
        Keyword.Word("digitalisation"),
        Keyword.Word("digitalization"),
        Keyword.Word("digitisation"),
        Keyword.Word("digitization"),
        Keyword.Word("e-governance"),
        Keyword.Word("e-government"),
        Keyword.Word("egov"),
        Keyword.Word("govtech"),
        Keyword.Word("cyber"),
        Keyword.Word("cybersecurity"),
        Keyword.Word("ICT", "ICTs"),
        Keyword.Word("information system", "information systems"),
        Keyword.Word("information technology", "information technologies"),
        Keyword.CaseSensitive("IT"),
        Keyword.Word("registry", "registries"),
        // просто register надто часто означає «зареєструвати» чи «реєстр учасників»
        Keyword.Word("state register", "state registers"),
        Keyword.Word("electronic register", "electronic registers"),
        Keyword.Word("unified register", "unified registers"),
        Keyword.Word("digital register", "digital registers"),
        Keyword.Word("open data"),
        Keyword.Word("interoperability"),
        Keyword.Word("Diia"),
        Keyword.Word("Trembita"),
        Keyword.Word("eHealth"),
        Keyword.Word("e-health"),
        Keyword.Word("e-services"),
        Keyword.Word("electronic services"),
        Keyword.Word("software"),
        Keyword.Word("data management"),
        Keyword.Word("broadband"),
        // просто connectivity в українських записах здебільшого про дороги й логістику
        // (Ukraine Transport Connectivity, RELINC), тому тільки разом з internet
        Keyword.Word("internet connectivity"),
        Keyword.Word("artificial intelligence"),
        Keyword.Stem("цифров"),
        Keyword.Stem("кібер"),
        Keyword.Stem("електронн"),
    ];

    public static IEnumerable<string> Find(string text) =>
        All.Where(k => k.IsIn(text)).Select(k => k.Text);
}
