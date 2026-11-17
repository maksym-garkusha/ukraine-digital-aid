using System.Globalization;
using UkraineDigitalAid.Activities;

namespace UkraineDigitalAid.Selection;

public enum Basis
{
    Sector,
    Title,
    Description
}

/// <param name="What">Що саме збіглося: код сектору з часткою або слово.</param>
public record Reason(Basis Basis, string What);

/// <param name="Reasons">Усі підстави, перша з них головна.</param>
/// <param name="Direct">Прямо про цифровізацію: частка ІКТ-секторів від 20% або слово в назві, і це не військова допомога.</param>
/// <param name="Military">Запис публікатора з DigitalRules.MilitaryPublishers: завжди дотичний.</param>
public record Selection(IReadOnlyList<Reason> Reasons, bool Direct, double IctShare, bool Military)
{
    public Basis Basis => Reasons[0].Basis;
}

/// <summary>
/// Евристика «цифровості» з README: сектор, слово в назві, слово лише в описі.
/// </summary>
public static class DigitalRules
{
    /// <summary>
    /// 22040 ІКТ, 22010 політика у сфері зв'язку, 22020 телекомунікації.
    /// 22030 (радіо, ТБ, друк) навмисно не входить: це медіа, а не цифровізація.
    /// </summary>
    public static readonly IReadOnlyList<string> SectorCodes = ["22040", "22010", "22020"];

    public const double DirectSectorShare = 20;

    /// <summary>
    /// Публікатори військової допомоги. Міністерство оборони США (US-GOV-7) подає в IATI
    /// постачання війську: програмне забезпечення до техніки, кіберпідтримку Міноборони,
    /// і слова «software» чи «cyber» у назві роблять їх прямими. Правило за сектором DAC 152
    /// (Conflict, peace and security) тут не годиться: туди ж донори відносять цивільну
    /// кібербезпеку органів влади, е-урядування SDC, цифрові докази для правосуддя.
    /// </summary>
    public static readonly IReadOnlyList<string> MilitaryPublishers = ["US-GOV-7"];

    public static Selection? Evaluate(Activity activity)
    {
        var sectors = activity.Sectors.Where(s => SectorCodes.Contains(s.Code)).ToList();
        double share = Math.Round(sectors.Sum(s => s.Percent), 2);
        var sectorReasons = sectors.Select(s => new Reason(Basis.Sector, $"{s.Code} {Percent(s.Percent)}")).ToList();

        var inTitle = Keywords.Find(string.Join('\n', activity.Titles.Select(t => t.Text))).ToList();
        var inDescription = Keywords.Find(string.Join('\n', activity.Descriptions.Select(d => d.Text)))
            .Except(inTitle).ToList();

        var reasons = new List<Reason>();
        bool strongSector = sectors.Count > 0 && share >= DirectSectorShare;
        if (strongSector)
            reasons.AddRange(sectorReasons);
        reasons.AddRange(inTitle.Select(w => new Reason(Basis.Title, w)));
        if (!strongSector)
            reasons.AddRange(sectorReasons);
        reasons.AddRange(inDescription.Select(w => new Reason(Basis.Description, w)));

        if (reasons.Count == 0)
            return null;
        bool military = activity.Publisher.Ref is { } publisher
            && MilitaryPublishers.Contains(publisher.Trim(), StringComparer.OrdinalIgnoreCase);
        return new Selection(reasons, (strongSector || inTitle.Count > 0) && !military, share, military);
    }

    private static string Percent(double value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
