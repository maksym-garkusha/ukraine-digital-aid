namespace UkraineDigitalAid.Activities;

/// <summary>Ролі participating-org за довідником IATI OrganisationRole.</summary>
public enum OrgRole
{
    Funding = 1,
    Accountable = 2,
    Extending = 3,
    Implementing = 4
}

public record Narrative(string Lang, string Text);

public record Organisation(string? Ref, string? Name);

/// <param name="Name">null, якщо донор приховав назву або вона схожа на ім'я людини.</param>
/// <param name="Generic">Замість організації вказано лише її категорію за довідником DAC
/// («Other multilateral institution»), тож зводити за нею не можна.</param>
public record Participant(OrgRole Role, string? Ref, string? Name, string? Type, bool Generic);

public record SectorShare(string Code, double Percent);

/// <summary>
/// Активність IATI в тому вигляді, який потрібен для відбору і сторінки.
/// Контактних даних тут немає навмисно: парсер їх не читає.
/// </summary>
/// <param name="Commitment">Зобов'язання за всю активність у доларах США, як їх рахує d-portal.</param>
/// <param name="Spend">Виплати і витрати за всю активність у доларах США.</param>
/// <param name="UkraineShare">Частка України у відсотках; null, якщо з запису її не видно.</param>
/// <param name="Sectors">Сектори DAC з п'ятизначними кодами та їхні частки.</param>
public record Activity(
    string Id,
    IReadOnlyList<Narrative> Titles,
    IReadOnlyList<Narrative> Descriptions,
    Organisation Publisher,
    IReadOnlyList<Participant> Participants,
    DateOnly? Start,
    DateOnly? End,
    int? Status,
    decimal? Commitment,
    decimal? Spend,
    IReadOnlyList<SectorShare> Sectors,
    double? UkraineShare,
    string? ParentId,
    IReadOnlyList<string> ChildIds)
{
    public string Title => Narratives.Preferred(Titles) ?? Id;

    public string? Description => Narratives.Preferred(Descriptions);

    public IEnumerable<Participant> WithRole(OrgRole role) => Participants.Where(p => p.Role == role);
}

public static class Narratives
{
    /// <summary>Англійський текст, якщо він є, інакше перший непорожній.</summary>
    public static string? Preferred(IReadOnlyList<Narrative> narratives) =>
        (narratives.FirstOrDefault(n => n.Lang == "en") ?? narratives.FirstOrDefault())?.Text;
}
