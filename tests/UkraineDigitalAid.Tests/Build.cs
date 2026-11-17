using UkraineDigitalAid.Activities;

namespace UkraineDigitalAid.Tests;

/// <summary>Активність для тестів правил і зведень: усе порожнє, крім того, що задав тест.</summary>
public static class Build
{
    public static Activity Activity(
        string id = "XM-TEST-1",
        string title = "Support to local communities",
        string? description = null,
        string publisher = "SE-0",
        IEnumerable<SectorShare>? sectors = null,
        IEnumerable<Participant>? participants = null,
        int? status = 2,
        DateOnly? end = null,
        decimal? commitment = null,
        decimal? spend = null,
        double? ukraineShare = 100,
        string? parentId = null) =>
        new(id,
            [new Narrative("en", title)],
            description is null ? [] : [new Narrative("en", description)],
            new Organisation(publisher, publisher + " name"),
            participants?.ToList() ?? [],
            new DateOnly(2023, 1, 1), end, status, commitment, spend,
            sectors?.ToList() ?? [], ukraineShare, parentId, []);

    public static Participant Implementer(string? name, string? reference = null) =>
        new(OrgRole.Implementing, reference, name, null, false);
}
