using UkraineDigitalAid.Activities;
using UkraineDigitalAid.Stats;

namespace UkraineDigitalAid.Tests;

public class StatsTests
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    private static Summary Summarise(params Activity[] set) => Stats.Stats.Summarise(set, Today);

    [Fact]
    public void Donor_sums_count_only_records_wholly_for_Ukraine()
    {
        var summary = Summarise(
            Build.Activity(id: "A-1", commitment: 1_000_000, spend: 400_000, ukraineShare: 100),
            Build.Activity(id: "A-2", commitment: 9_000_000, spend: 9_000_000, ukraineShare: 12.5));

        var donor = summary.Donors.Single();
        Assert.Equal(2, donor.Total);
        Assert.Equal(1_000_000, donor.WholeCommitment);
        Assert.Equal(400_000, donor.WholeSpend);
        Assert.Equal(1, donor.WholeCount);
    }

    [Fact]
    public void Unknown_Ukraine_share_is_not_counted_as_whole()
    {
        var summary = Summarise(Build.Activity(commitment: 500, ukraineShare: null));

        Assert.Equal(0, summary.Donors.Single().WholeCommitment);
    }

    [Fact]
    public void Programme_with_selected_parts_is_not_counted_twice()
    {
        var summary = Summarise(
            Build.Activity(id: "SE-0-SE-22-68067", commitment: 250_000),
            Build.Activity(id: "SE-0-SE-22-68067-1", commitment: 250_000, parentId: "SE-0-SE-22-68067"));

        Assert.Equal(1, summary.Counted);
        Assert.Equal(250_000, summary.Donors.Single().WholeCommitment);
    }

    [Fact]
    public void Part_whose_programme_is_not_selected_is_counted()
    {
        var summary = Summarise(Build.Activity(id: "GB-GOV-1-400210-403", parentId: "GB-GOV-1-400210"));

        Assert.Equal(1, summary.Counted);
    }

    [Fact]
    public void Ending_soon_means_in_implementation_and_ending_within_a_year()
    {
        var summary = Summarise(
            Build.Activity(id: "A-1", status: 2, end: Today.AddMonths(6)),
            Build.Activity(id: "A-2", status: 2, end: Today.AddMonths(18)),
            Build.Activity(id: "A-3", status: 4, end: Today.AddMonths(3)),
            Build.Activity(id: "A-4", status: 2, end: Today.AddMonths(-3)));

        Assert.Equal(1, summary.EndingSoon);
        Assert.Equal(3, summary.Active);
        Assert.Equal(1, summary.ActivePastEnd);
    }

    [Fact]
    public void Implementers_group_by_identifier_and_take_the_common_name()
    {
        var summary = Summarise(
            Build.Activity(id: "A-1", publisher: "XI-IATI-EC_NEAR", participants: [Build.Implementer("United Nations Development Programme", "XM-DAC-41114")]),
            Build.Activity(id: "A-2", publisher: "CA-3", participants: [Build.Implementer("UNDP - United Nations Development Programme", "XM-DAC-41114")]),
            Build.Activity(id: "A-3", publisher: "SE-0", participants: [Build.Implementer("United Nations Development Programme", "XM-DAC-41114")]));

        var undp = summary.Implementers.Single();
        Assert.Equal("United Nations Development Programme", undp.Name);
        Assert.Equal(3, undp.Activities);
        Assert.Equal(["CA-3", "SE-0", "XI-IATI-EC_NEAR"], undp.Donors);
    }

    [Fact]
    public void Implementers_without_usable_identifier_group_by_name()
    {
        // 1016076 це внутрішній номер постачальника FCDO, а не ідентифікатор IATI
        var summary = Summarise(
            Build.Activity(id: "A-1", participants: [Build.Implementer("Goethe-Institut")]),
            Build.Activity(id: "A-2", participants: [Build.Implementer("GOETHE-INSTITUT ", "1016076")]));

        Assert.Equal(2, summary.Implementers.Single().Activities);
    }

    [Fact]
    public void Organisation_categories_and_hidden_names_are_not_implementers()
    {
        var summary = Summarise(Build.Activity(participants: [
            new Participant(OrgRole.Implementing, "XM-DAC-23000", "Developing country-based NGO", null, true),
            Build.Implementer(null, "XM-DAC-61000"),
            Build.Implementer("Uncoded"),
            Build.Implementer("International NGO")]));

        Assert.Empty(summary.Implementers);
    }

    [Theory]
    [InlineData("XM-DAC-41114", true)]
    [InlineData("GB-COH-1762840", true)]
    [InlineData("44001", true)]
    [InlineData("1", false)]
    [InlineData("TF612001", false)]
    [InlineData("UA–P0053693", false)]
    public void Only_iati_like_references_are_used_for_grouping(string reference, bool usable) =>
        Assert.Equal(usable, Organisations.LooksLikeIdentifier(reference));
}
