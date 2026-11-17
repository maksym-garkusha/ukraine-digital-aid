using UkraineDigitalAid.Selection;

namespace UkraineDigitalAid.Tests;

public class KeywordTests
{
    private static bool Found(string text, string keyword) => Keywords.Find(text).Contains(keyword);

    [Theory]
    [InlineData("Supply of IT hardware for the State Service")]
    [InlineData("IT-skills planning grant")]
    [InlineData("Mobility Scheme for Ukrainian IT Students")]
    public void Capital_IT_as_a_word_matches(string title) => Assert.True(Found(title, "IT"));

    [Theory]
    [InlineData("Humanitarian aid from ITALY")]
    [InlineData("Make it work for communities")]
    [InlineData("ITEM procurement")]
    public void IT_inside_another_word_or_lowercase_does_not_match(string title) => Assert.False(Found(title, "IT"));

    [Fact]
    public void Register_alone_does_not_match()
    {
        Assert.Empty(Keywords.Find("Help displaced people register for assistance"));
    }

    [Fact]
    public void Register_with_qualifier_matches()
    {
        Assert.True(Found("Support to the Unified State Register of Court Decisions", "state register"));
    }

    [Fact]
    public void Keyword_matches_case_insensitively()
    {
        Assert.True(Found("Strengthening CYBERSECURITY of critical infrastructure", "cybersecurity"));
    }

    [Fact]
    public void Plural_form_matches()
    {
        Assert.True(Found("Modernisation of health information systems", "information system"));
        Assert.True(Found("Support to national registries", "registry"));
    }

    [Fact]
    public void Plural_is_searched_separately_on_the_portal()
    {
        // text_search не знає основ: «information system» не знаходить «information systems»
        Assert.Equal(["information system", "information systems"], Keyword.Word("information system", "information systems").SearchTerms);
    }

    [Fact]
    public void Transport_connectivity_is_not_digital()
    {
        Assert.Empty(Keywords.Find("Ukraine Transport Connectivity - Phase I"));
        Assert.True(Found("Restoring internet connectivity in frontline communities", "internet connectivity"));
    }

    [Fact]
    public void Longer_word_does_not_match_its_beginning()
    {
        Assert.False(Keyword.Word("digital").IsIn("digitally signed documents"));
        Assert.False(Keyword.Word("cyber").IsIn("cyberattacks"));
    }

    [Fact]
    public void Hyphen_counts_as_word_boundary()
    {
        Assert.True(Keyword.Word("cyber").IsIn("protection against cyber-attacks"));
    }

    [Fact]
    public void Non_breaking_hyphen_and_line_break_do_not_hide_a_phrase()
    {
        Assert.True(Found("Support to e‑governance", "e-governance"));
        Assert.True(Found("national information\nsystem", "information system"));
    }

    [Fact]
    public void Ukrainian_stem_matches_inflected_forms()
    {
        Assert.True(Found("Підтримка цифрової трансформації громад", "цифров"));
        Assert.True(Found("КІБЕРБЕЗПЕКА державних реєстрів", "кібер"));
    }

    [Fact]
    public void Ukrainian_stem_needs_word_start()
    {
        Assert.False(Keyword.Stem("кібер").IsIn("антикібервійна"));
    }

    [Fact]
    public void Stems_and_IT_are_not_sent_to_full_text_search()
    {
        // d-portal шукає цілі слова в нижньому регістрі: «it» збігся б з англійським займенником
        Assert.Empty(Keyword.CaseSensitive("IT").SearchTerms);
        Assert.Empty(Keyword.Stem("цифров").SearchTerms);
        Assert.Equal(["open data"], Keyword.Word("open data").SearchTerms);
    }
}
