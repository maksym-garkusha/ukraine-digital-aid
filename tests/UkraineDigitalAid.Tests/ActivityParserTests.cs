using System.Text.Json;
using UkraineDigitalAid.Activities;
using UkraineDigitalAid.Iati;

namespace UkraineDigitalAid.Tests;

public class ActivityParserTests
{
    private static Activity Parse(string fixture, ListRow? row = null) => ActivityParser.Parse(Fixture.Record(fixture), row);

    [Fact]
    public void World_bank_record_keeps_only_dac_sectors_without_duplicates()
    {
        // у записі кожен сектор DAC повторено двічі, поруч власна таксономія банку (словник 99)
        var activity = Parse("world-bank");

        Assert.Equal([new SectorShare("24010", 76), new SectorShare("25010", 24)], activity.Sectors);
    }

    [Fact]
    public void Contact_person_is_not_read()
    {
        var activity = Parse("world-bank");

        string stored = JsonSerializer.Serialize(activity);
        Assert.DoesNotContain("Testenko", stored);
        Assert.DoesNotContain("example.org", stored);
        Assert.DoesNotContain("+380", stored);
    }

    [Fact]
    public void Actual_end_date_wins_over_planned()
    {
        // планове завершення 2026-05-06, фактичне 2025-09-01
        var activity = Parse("undp");

        Assert.Equal(new DateOnly(2019, 1, 1), activity.Start);
        Assert.Equal(new DateOnly(2025, 9, 1), activity.End);
    }

    [Fact]
    public void Planned_dates_are_used_when_there_are_no_actual_ones()
    {
        var activity = Parse("german-ffo");

        Assert.Equal(new DateOnly(2019, 6, 1), activity.Start);
        Assert.Equal(new DateOnly(2019, 12, 31), activity.End);
    }

    [Fact]
    public void Dates_from_the_list_fill_gaps_in_the_record()
    {
        var record = Fixture.Json("""{"/iati-identifier":"X-1","/activity-status@code":"2"}""");
        var row = new ListRow("X-1", null, null, null, 2, 19083, 19265, null, null, 100);

        var activity = ActivityParser.Parse(record, row);

        Assert.Equal(new DateOnly(2022, 4, 1), activity.Start);
        Assert.Equal(new DateOnly(2022, 9, 30), activity.End);
    }

    [Fact]
    public void Titles_keep_every_language_and_prefer_english()
    {
        var activity = Parse("bmz-giz");

        Assert.Equal(["en", "de"], activity.Titles.Select(t => t.Lang));
        Assert.Equal("Good Governance through GovTech and Transparency", activity.Title);
    }

    [Fact]
    public void Narrative_without_language_takes_the_activity_language()
    {
        var record = Fixture.Json("""
            {"/iati-identifier":"X-1","@xml:lang":"FR","/title/narrative":[{"":"Appui numérique"}]}
            """);

        Assert.Equal("fr", ActivityParser.Parse(record).Titles.Single().Lang);
    }

    [Fact]
    public void Repeated_narratives_are_dropped()
    {
        // USAID повторює ту саму іспанську назву тричі
        var record = Fixture.Json("""
            {"/iati-identifier":"X-1","/title/narrative":[
              {"":"Digital Services","@xml:lang":"EN"},
              {"":"Servicios Digitales","@xml:lang":"ES"},
              {"":"Servicios Digitales","@xml:lang":"ES"}]}
            """);

        Assert.Equal(2, ActivityParser.Parse(record).Titles.Count);
    }

    [Fact]
    public void Sectors_come_from_transactions_when_activity_has_none()
    {
        var activity = Parse("sida-child");

        Assert.Equal([new SectorShare("22040", 100)], activity.Sectors);
    }

    [Fact]
    public void Transaction_sectors_ignore_other_vocabularies()
    {
        // у транзакціях USAID поруч з 22040 стоїть код 5004 зі словника 99
        var activity = Parse("usaid-redacted");

        Assert.Equal([new SectorShare("22040", 100)], activity.Sectors);
    }

    [Fact]
    public void Single_sector_without_percentage_counts_as_whole()
    {
        var record = Fixture.Json("""{"/iati-identifier":"X-1","/sector":[{"@code":"22040"}]}""");

        Assert.Equal(100, ActivityParser.Parse(record).Sectors.Single().Percent);
    }

    [Fact]
    public void Ukraine_share_is_its_percentage_among_several_countries()
    {
        Assert.Equal(12.5, Parse("fcdo-multi-country").UkraineShare);
        Assert.Equal(3, Parse("ebrd-regional").UkraineShare);
    }

    [Fact]
    public void Ukraine_share_comes_from_transactions_when_activity_lists_no_country()
    {
        Assert.Equal(100, Parse("usaid-redacted").UkraineShare);
    }

    [Fact]
    public void Several_countries_without_percentages_give_unknown_share()
    {
        var record = Fixture.Json("""
            {"/iati-identifier":"X-1","/recipient-country":[{"@code":"UA"},{"@code":"MD"}]}
            """);

        Assert.Null(ActivityParser.Parse(record).UkraineShare);
    }

    [Fact]
    public void Implementing_organisations_keep_their_identifiers()
    {
        var activity = Parse("eu-contract");

        var implementer = activity.WithRole(OrgRole.Implementing).Single();
        Assert.Equal("E-RIIGI AKADEEMIA SIHTASUTUS*E-GOVERNANCE ACADEMY EGA", implementer.Name);
        Assert.Null(implementer.Ref);
    }

    [Fact]
    public void Organisation_category_instead_of_organisation_is_marked_generic()
    {
        var accountable = Parse("eu-contract").WithRole(OrgRole.Accountable).Single();

        Assert.Equal("XM-DAC-61000", accountable.Ref);
        Assert.True(accountable.Generic);
    }

    [Fact]
    public void Redacted_organisation_name_is_not_kept()
    {
        var record = Fixture.Json("""
            {"/iati-identifier":"X-1","/participating-org":[{"@role":"4","@ref":"XM-DAC-61000",
              "/narrative":[{"":"USAID redacted this field in accordance with the exceptions outlined in the Foreign Aid Transparency and Accountability Act of 2016."}]}]}
            """);

        Assert.Null(ActivityParser.Parse(record).Participants.Single().Name);
    }

    [Fact]
    public void Contractor_named_like_a_person_is_not_kept()
    {
        var names = Parse("fcdo-multi-country").WithRole(OrgRole.Implementing).Select(p => p.Name).ToList();

        Assert.DoesNotContain("IVAN TESTENKO", names);
        Assert.Contains("THE ALAN TURING INSTITUTE", names);
        Assert.Contains("PRICEWATERHOUSECOOPERS LLP", names);
    }

    [Fact]
    public void Child_record_points_to_its_parent()
    {
        Assert.Equal("SE-0-SE-22-68067", Parse("sida-child").ParentId);
        Assert.Equal(["SE-0-SE-22-68067-1"], Parse("sida-parent").ChildIds);
    }

    [Fact]
    public void Sums_come_from_the_list_row()
    {
        var row = new ListRow("44000-P505616", null, null, null, 2, null, null, 1061000000m, 565008300m, 100);

        var activity = Parse("world-bank", row);

        Assert.Equal(1061000000m, activity.Commitment);
        Assert.Equal(565008300m, activity.Spend);
    }

    [Fact]
    public void Empty_description_of_second_type_is_skipped()
    {
        var activity = Parse("world-bank");

        Assert.Single(activity.Descriptions);
        Assert.StartsWith("The development objective", activity.Description);
    }

    [Fact]
    public void Emails_in_description_are_hidden()
    {
        Assert.Equal("Contact [e-mail] for details", ActivityParser.HideEmails("Contact someone@example.org for details"));
    }

    [Fact]
    public void Path_finds_value_whether_xson_flattens_it_or_not()
    {
        var flat = Fixture.Json("""{"/reporting-org@ref":"44000"}""");
        var nested = Fixture.Json("""{"/reporting-org":[{"@ref":"44000"}]}""");

        Assert.Equal("44000", Xson.String(flat, "/reporting-org@ref"));
        Assert.Equal("44000", Xson.String(nested, "/reporting-org@ref"));
    }

    [Fact]
    public void Status_is_read_from_the_record()
    {
        Assert.Equal(3, Parse("undp").Status);
    }
}
