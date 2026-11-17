namespace UkraineDigitalAid.Selection;

/// <summary>
/// Перелік слів для відбору «цифрових» активностей. Правити тут: усе інше,
/// зокрема пошук кандидатів на d-portal і README, бере слова з цього списку.
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
        Keyword.Word("ICT"),
        Keyword.Word("information system"),
        Keyword.Word("information technology"),
        Keyword.CaseSensitive("IT"),
        Keyword.Word("registry"),
        Keyword.Word("registries"),
        // просто register надто часто означає «реєстр учасників» чи «зареєструвати»
        Keyword.Word("state register"),
        Keyword.Word("electronic register"),
        Keyword.Word("unified register"),
        Keyword.Word("digital register"),
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
        Keyword.Word("connectivity"),
        Keyword.Word("artificial intelligence"),
        Keyword.Stem("цифров"),
        Keyword.Stem("кібер"),
        Keyword.Stem("електронн"),
    ];

    public static IEnumerable<string> Find(string text) =>
        All.Where(k => k.IsIn(text)).Select(k => k.Text);
}
