namespace UkraineDigitalAid.Activities;

/// <summary>
/// Деякі донори вписують у participating-org підрядників-фізосіб, тобто ім'я людини.
/// Таку назву не зберігаємо. Ознака груба: без ідентифікатора організації,
/// два-чотири слова з літер і жодного слова, типового для назви організації.
/// Помилка в бік обережності лише ховає назву організації, тому це прийнятно.
/// </summary>
public static class PersonNames
{
    private static readonly HashSet<string> OrganisationWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "of", "for", "and", "the", "de", "des", "du", "la", "le", "et", "für", "und", "der", "die", "y", "del",
        "&", "ltd", "llc", "llp", "lp", "inc", "plc", "pty", "pvt", "sarl", "sas", "spa", "kft", "aps", "gmbh", "ag", "sa", "bv", "nv", "ab", "as", "oy", "srl", "sp", "z", "o.o.",
        "co", "corp", "corporation", "company", "group", "holding", "limited", "partners", "partnership",
        "consulting", "consultants", "services", "solutions", "systems", "technologies", "technology", "tech",
        "agency", "authority", "bank", "board", "bureau", "center", "centre", "chamber", "college", "commission",
        "committee", "council", "department", "directorate", "embassy", "fund", "foundation", "government",
        "institute", "institution", "ministry", "network", "office", "organisation", "organization", "programme",
        "program", "project", "secretariat", "service", "society", "trust", "union", "university", "academy",
        "association", "alliance", "initiative", "federation", "coalition", "club", "school", "hospital",
        "media", "news", "press", "radio", "tv", "studio", "lab", "labs", "hub", "platform", "cooperative",
        "ngo", "cso", "charity", "mission", "unit", "team", "forum", "house", "world", "global", "international",
        "national", "europe", "european", "ukraine", "ukrainian", "kyiv", "america", "american", "nations",
        "united", "un", "eu", "usa", "uk", "public", "private", "state", "city", "region", "regional", "local"
    };

    public static bool LooksLikePerson(string name, string? reference)
    {
        if (!string.IsNullOrWhiteSpace(reference))
            return false;
        var words = name.Split((char[])[' ', '\t', '\n', ','], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is < 2 or > 4)
            return false;
        return words.All(w => w.Length >= 2 && w.All(c => char.IsLetter(c) || c is '-' or '\'' or '’' or '.')
                              && !OrganisationWords.Contains(w.Trim('.')));
    }
}
