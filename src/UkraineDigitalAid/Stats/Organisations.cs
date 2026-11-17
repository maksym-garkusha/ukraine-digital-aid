using System.Text.RegularExpressions;
using UkraineDigitalAid.Activities;

namespace UkraineDigitalAid.Stats;

/// <summary>
/// За чим зводити організації. Ідентифікатор IATI надійніший за назву, але
/// донори пишуть у @ref усе підряд: власні номери постачальників («1016076»),
/// «1» замість коду USAID, коди з довгим тире. Тому за @ref зводимо, тільки коли
/// він схожий на ідентифікатор організації IATI, інакше за назвою.
/// Назви лише обрізаються й зводяться до одного регістру, без злиття схожих.
/// </summary>
public static partial class Organisations
{
    // так донори підписують виконавця, коли називають лише тип організації або нічого
    private static readonly HashSet<string> GenericNames =
    [
        "UNCODED", "UNKNOWN", "NOT APPLICABLE", "N/A", "OTHER", "INTERNATIONAL NGO", "NATIONAL NGO", "REGIONAL NGO",
        "PARTNER COUNTRY BASED NGO", "DONOR COUNTRY-BASED NGO", "DEVELOPING COUNTRY-BASED NGO", "DEV. COUNTRY BASED NGOS",
        "PRIVATE SECTOR IN PROVIDER COUNTRY", "PRIVATE SECTOR IN RECIPIENT COUNTRY", "PRIVATE SECTOR IN THIRD COUNTRY",
        "OTHER MULTILATERAL INSTITUTION"
    ];

    /// <summary>Замість організації вказано її тип: кодом категорії DAC або словами.</summary>
    public static bool IsGeneric(Participant participant) =>
        participant.Generic || (participant.Name is { } name && GenericNames.Contains(NormalizeName(name)));

    public static string? Key(Participant participant)
    {
        if (IsGeneric(participant))
            return null;
        if (participant.Ref is { } reference && LooksLikeIdentifier(reference))
            return "ref:" + reference.ToUpperInvariant();
        return participant.Name is { } name ? "name:" + NormalizeName(name) : null;
    }

    public static bool LooksLikeIdentifier(string reference) => Identifier().IsMatch(reference.Trim());

    /// <summary>
    /// Назва публікатора для показу. Відомства уряду США (ідентифікатори US-GOV-...)
    /// підписуються без країни, «Department of Defense», тож додаємо «U.S.».
    /// </summary>
    public static string PublisherName(Organisation publisher)
    {
        if (publisher.Name?.Trim() is not { Length: > 0 } name)
            return publisher.Ref ?? "";
        bool usGovernment = publisher.Ref?.StartsWith("US-GOV-", StringComparison.OrdinalIgnoreCase) == true;
        bool hasCountry = name.StartsWith("U.S.", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("US ", StringComparison.Ordinal)
            || name.StartsWith("United States", StringComparison.OrdinalIgnoreCase);
        return usGovernment && !hasCountry ? "U.S. " + name : name;
    }

    public static string NormalizeName(string name) =>
        string.Join(' ', name.Split((char[])[' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    // префікс агенції реєстрації (XM-DAC-41114, GB-COH-123, UA-EDR-...) або п'ятизначний код каналу DAC (44001)
    [GeneratedRegex(@"^([A-Z]{2}-[A-Z0-9_]+(-[A-Za-z0-9_./-]+)?|\d{5})$")]
    private static partial Regex Identifier();
}
