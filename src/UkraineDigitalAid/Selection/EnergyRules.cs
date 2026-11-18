using System.Globalization;
using UkraineDigitalAid.Activities;

namespace UkraineDigitalAid.Selection;

/// <param name="Share">Сума часток секторів DAC з групи 230 Energy.</param>
/// <param name="Why">Що збіглося: «23630 100%» або слово; сектори йдуть першими.</param>
/// <param name="WorthChecking">Енергетичного сектору менше за EnergyRules.SectorShare, і запис знайдено
/// лише за словами: на сторінці він позначений «варто перевірити».</param>
public record EnergyMatch(double Share, IReadOnlyList<string> Why, bool WorthChecking);

/// <summary>
/// Тема «енергетика» серед уже відібраних цифрових записів. Надійна підстава тут сектор DAC
/// з групи 230 Energy, слова в назві чи описі лише підказка.
/// </summary>
public static class EnergyRules
{
    /// <summary>
    /// Частка, з якої сектор сам робить запис енергетичним. Меншу частку донори ставлять
    /// службовим записам (USAID «Travel and Transportation» з 0,09% у 23110) і багатогалузевим
    /// рамкам ЄС з 10% на все підряд.
    /// </summary>
    public const double SectorShare = 20;

    public static readonly IReadOnlyList<Keyword> TitleWords =
    [
        Keyword.Word("energy"),
        Keyword.Word("electricity"),
        Keyword.Word("power grid"),
        Keyword.Word("power plant"),
        Keyword.Word("power transmission"),
        Keyword.Word("hydropower"),
        Keyword.Word("nuclear"),
        Keyword.Word("renewable"),
        Keyword.Word("solar"),
        Keyword.Word("heating"),
        Keyword.Word("natural gas"),
        Keyword.Word("substation"),
        Keyword.Word("Ukrenergo"),
        Keyword.Word("Energoatom"),
        Keyword.Word("Naftogaz"),
        Keyword.Stem("енерг"),
        Keyword.Stem("електроенерг"),
        Keyword.Stem("теплопостач"),
    ];

    /// <summary>
    /// В описах просте energy трапляється в переліках галузей («energy, transport, agri-food»)
    /// і в однаковому абзаці про енергетичну безпеку в десятках записів DG NEAR, які не про енергетику.
    /// Тому в описі шукаються словосполучення, з яких видно предмет.
    /// </summary>
    public static readonly IReadOnlyList<Keyword> DescriptionWords =
    [
        Keyword.Word("energy sector"),
        Keyword.Word("energy infrastructure"),
        Keyword.Word("energy system"),
        Keyword.Word("energy supply"),
        Keyword.Word("energy transition"),
        Keyword.Word("renewable energy"),
        Keyword.Word("electricity"),
        Keyword.Word("power grid"),
        Keyword.Word("power plant"),
        Keyword.Word("power supply"),
        Keyword.Word("power transmission"),
        Keyword.Word("substation"),
        Keyword.Word("hydropower"),
        Keyword.Word("district heating"),
        Keyword.Word("nuclear power"),
        Keyword.Word("nuclear safety"),
        Keyword.Word("transmission system operator"),
        Keyword.Word("Ukrenergo"),
        Keyword.Word("Energoatom"),
        Keyword.Word("Naftogaz"),
        Keyword.Stem("енергетичн"),
        Keyword.Stem("електроенерг"),
    ];

    /// <summary>
    /// Група 230 у довіднику DAC: 231 політика, 232 відновлювані джерела, 233 невідновлювані,
    /// 234 гібридні станції, 235 атомна енергетика і ядерна безпека, 236 передача й розподіл.
    /// </summary>
    public static bool IsEnergySector(string code) => code.Length == 5 && code.StartsWith("23", StringComparison.Ordinal);

    public static EnergyMatch? Evaluate(Activity activity)
    {
        var sectors = activity.Sectors.Where(s => IsEnergySector(s.Code)).ToList();
        double share = Math.Round(sectors.Sum(s => s.Percent), 2);

        string titles = string.Join('\n', activity.Titles.Select(t => t.Text));
        string descriptions = string.Join('\n', activity.Descriptions.Select(d => d.Text));
        var words = TitleWords.Where(k => k.IsIn(titles))
            .Concat(DescriptionWords.Where(k => k.IsIn(descriptions)))
            .Select(k => k.Text)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        bool bySector = share >= SectorShare;
        if (!bySector && words.Count == 0)
            return null;
        var why = sectors.Select(s => $"{s.Code} {s.Percent.ToString("0.##", CultureInfo.InvariantCulture)}%").Concat(words).ToList();
        return new EnergyMatch(share, why, !bySector);
    }
}
