using UkraineDigitalAid.Activities;

namespace UkraineDigitalAid.Tests;

public class PersonNamesTests
{
    [Theory]
    [InlineData("IVAN TESTENKO")]
    [InlineData("Olena Testenko-Prykladna")]
    public void Two_or_three_plain_words_look_like_a_person(string name) =>
        Assert.True(PersonNames.LooksLikePerson(name, null));

    [Theory]
    [InlineData("Eurasia Foundation")]
    [InlineData("PRICEWATERHOUSECOOPERS LLP")]
    [InlineData("Ministry of Digital Transformation of Ukraine")]
    [InlineData("Swedfund")]
    public void Organisation_names_do_not_look_like_a_person(string name) =>
        Assert.False(PersonNames.LooksLikePerson(name, null));

    [Fact]
    public void Name_with_organisation_identifier_is_never_a_person() =>
        Assert.False(PersonNames.LooksLikePerson("Alan Turing", "GB-COH-09512457"));
}
