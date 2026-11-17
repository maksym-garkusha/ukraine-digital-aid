using UkraineDigitalAid.Iati;

namespace UkraineDigitalAid.Tests;

public class CodelistsTests
{
    // скорочений фрагмент Sector.json з сайту IATI
    private const string Sector = """
        {"attributes":{"name":"Sector"},"metadata":{},"data":[
          {"code":"22040","category":"220","name":"Information and communication technology (ICT)","status":"active"},
          {"code":"15110","category":"151","name":"Public sector policy and administrative management","status":"active"}]}
        """;

    [Fact]
    public void Codelist_maps_code_to_name()
    {
        var sectors = Codelists.Parse(Sector);

        Assert.Equal("Information and communication technology (ICT)", sectors["22040"]);
        Assert.Equal(2, sectors.Count);
    }

    [Fact]
    public void Missing_codelist_files_give_empty_lists()
    {
        var lists = Codelists.Read(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        Assert.Empty(lists.Sectors);
        Assert.Empty(lists.Statuses);
    }
}
