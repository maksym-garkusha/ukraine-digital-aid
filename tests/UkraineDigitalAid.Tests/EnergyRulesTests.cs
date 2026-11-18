using UkraineDigitalAid.Activities;
using UkraineDigitalAid.Selection;

namespace UkraineDigitalAid.Tests;

public class EnergyRulesTests
{
    private static EnergyMatch? Evaluate(string title, string? description = null, params SectorShare[] sectors) =>
        EnergyRules.Evaluate(Build.Activity(title: title, description: description, sectors: sectors));

    [Fact]
    public void Energy_sector_makes_the_record_energy_without_a_check()
    {
        var match = Evaluate("Second Power Transmission Project", null, new SectorShare("23630", 100));

        Assert.False(match!.WorthChecking);
        Assert.Equal(100, match.Share);
        Assert.Equal(["23630 100%", "power transmission"], match.Why);
    }

    [Fact]
    public void Shares_of_the_whole_energy_group_add_up()
    {
        var match = Evaluate("Support", null, new SectorShare("23110", 12), new SectorShare("23510", 10), new SectorShare("22040", 78));

        Assert.Equal(22, match!.Share);
        Assert.False(match.WorthChecking);
    }

    [Fact]
    public void Small_energy_share_alone_is_not_energy()
    {
        Assert.Null(Evaluate("USAID Travel and Transportation", null, new SectorShare("23110", 0.09), new SectorShare("22040", 11)));
    }

    [Fact]
    public void Codes_outside_group_230_are_not_energy()
    {
        Assert.Null(Evaluate("Support", null, new SectorShare("22040", 50), new SectorShare("21020", 50)));
    }

    [Fact]
    public void Word_in_title_alone_is_worth_checking()
    {
        var match = Evaluate("Green Cities, Infrastructure and Energy - Ukraine");

        Assert.True(match!.WorthChecking);
        Assert.Equal(0, match.Share);
        Assert.Equal(["energy"], match.Why);
    }

    [Fact]
    public void Small_share_with_a_word_is_worth_checking_and_shows_the_share()
    {
        var match = Evaluate("Activity under preparation Better Services for Consumers in the Reformed Energy Markets", null,
            new SectorShare("23110", 10));

        Assert.True(match!.WorthChecking);
        Assert.Equal(["23110 10%", "energy"], match.Why);
    }

    [Fact]
    public void Plain_energy_in_description_does_not_count()
    {
        // абзац, який DG NEAR повторює в описах записів про медіа, житло, статистику
        Assert.Null(Evaluate("Supporting independent local media outlets",
            "The Action supports the Government in its strategic planning as well as energy security."));
    }

    [Fact]
    public void Phrase_in_description_is_worth_checking()
    {
        var match = Evaluate("Support to strengthen cybersecurity in Ukraine's civilian critical infrastructure",
            "Improving the cyber security and resilience of Ukrainian energy infrastructure.");

        Assert.True(match!.WorthChecking);
        Assert.Equal(["energy infrastructure"], match.Why);
    }

    [Fact]
    public void Nuclear_counts_in_title_but_not_alone_in_description()
    {
        Assert.NotNull(Evaluate("Strengthening Competencies for Sustainable Nuclear Security"));
        Assert.Null(Evaluate("Environmental determinants of health", "Chemical and radio-nuclear events, poison centres."));
    }

    [Fact]
    public void Ukrainian_stem_is_found_at_the_start_of_a_word()
    {
        Assert.NotNull(Evaluate("Цифровізація енергетичного сектору"));
        // «енерг» усередині слова не рахується, для цього є окрема основа «електроенерг»
        Assert.Equal(["електроенерг"], Evaluate("Облік електроенергії в громадах")!.Why);
    }

    [Fact]
    public void Same_word_in_title_and_description_is_listed_once()
    {
        var match = Evaluate("Electricity market data platform", "Data exchange for the electricity market.");

        Assert.Equal(["electricity"], match!.Why);
    }
}
