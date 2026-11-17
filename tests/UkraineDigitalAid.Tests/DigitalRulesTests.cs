using UkraineDigitalAid.Activities;
using UkraineDigitalAid.Selection;

namespace UkraineDigitalAid.Tests;

public class DigitalRulesTests
{
    private static Selection.Selection? Evaluate(string title, string? description = null, params SectorShare[] sectors) =>
        DigitalRules.Evaluate(Build.Activity(title: title, description: description, sectors: sectors));

    [Fact]
    public void Ict_sector_of_at_least_a_fifth_is_direct()
    {
        var selection = Evaluate("Technical Assistance Ukraine", null, new SectorShare("22040", 20), new SectorShare("15110", 80));

        Assert.True(selection!.Direct);
        Assert.Equal(Basis.Sector, selection.Basis);
        Assert.Equal("22040 20%", selection.Reasons[0].What);
    }

    [Fact]
    public void Ict_sector_shares_add_up()
    {
        var selection = Evaluate("Support", null, new SectorShare("22040", 12), new SectorShare("22020", 10));

        Assert.Equal(22, selection!.IctShare);
        Assert.True(selection.Direct);
    }

    [Fact]
    public void Small_ict_share_without_keyword_is_tangential()
    {
        var selection = Evaluate("USAID Pay and Benefits", null, new SectorShare("22040", 11.8), new SectorShare("15110", 88.2));

        Assert.False(selection!.Direct);
        Assert.Equal(Basis.Sector, selection.Basis);
    }

    [Fact]
    public void Media_sector_is_not_digital()
    {
        Assert.Null(Evaluate("Support to independent media", null, new SectorShare("22030", 100)));
    }

    [Fact]
    public void Keyword_in_title_is_direct()
    {
        var selection = Evaluate("EU4DigitalUA: interoperability, e-services and cybersecurity");

        Assert.True(selection!.Direct);
        Assert.Equal(Basis.Title, selection.Basis);
        // «Digital» усередині EU4DigitalUA не окреме слово, тому не рахується
        Assert.Equal(["cybersecurity", "interoperability", "e-services"], selection.Reasons.Select(r => r.What));
    }

    [Fact]
    public void Title_goes_before_small_ict_share()
    {
        var selection = Evaluate("Digital Partnership Week", null, new SectorShare("22040", 5));

        Assert.Equal(Basis.Title, selection!.Basis);
        Assert.Equal(Basis.Sector, selection.Reasons[1].Basis);
    }

    [Fact]
    public void Keyword_only_in_description_is_tangential()
    {
        var selection = Evaluate("Human Rights for Ukraine", "Support to the Ombudsperson, including its information system.");

        Assert.False(selection!.Direct);
        Assert.Equal(new Reason(Basis.Description, "information system"), selection.Reasons.Single());
    }

    [Fact]
    public void Word_in_title_is_not_repeated_as_description_reason()
    {
        var selection = Evaluate("Digital media marathon", "Training on digital tools.");

        Assert.All(selection!.Reasons, r => Assert.Equal(Basis.Title, r.Basis));
    }

    [Fact]
    public void Digital_only_in_organisation_name_is_not_selected()
    {
        var activity = Build.Activity(title: "Procurement of generators",
            participants: [Build.Implementer("Ministry of Digital Transformation of Ukraine")]);

        Assert.Null(DigitalRules.Evaluate(activity));
    }

    [Fact]
    public void Nothing_digital_is_not_selected()
    {
        Assert.Null(Evaluate("Winterisation support for displaced families", "Blankets and heaters.", new SectorShare("72010", 100)));
    }
}
