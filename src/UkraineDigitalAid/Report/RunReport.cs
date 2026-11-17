using System.Globalization;
using System.Text;
using UkraineDigitalAid.Activities;
using UkraineDigitalAid.Iati;
using UkraineDigitalAid.Selection;
using UkraineDigitalAid.Stats;
using UkraineDigitalAid.Store;

namespace UkraineDigitalAid.Report;

/// <summary>
/// Звіт у Markdown: скільки зібрано, що відібрано і за якою підставою, хто з
/// донорів і виконавців трапляється найчастіше, і які особливості даних видно з самих записів.
/// </summary>
public static class RunReport
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    public static string Build(IEnumerable<Activity> activities, FetchRun run, Codelists codelists)
    {
        var today = DateOnly.FromDateTime(run.FetchedAt);
        var selected = Stats.Stats.Select(activities);
        var direct = selected.Where(s => s.Selection.Direct).Select(s => s.Activity).ToList();
        var all = selected.Select(s => s.Activity).ToList();
        var summary = Stats.Stats.Summarise(direct, today);
        var md = new StringBuilder();

        md.AppendLine("# Звіт про зібрані дані");
        md.AppendLine();
        md.AppendLine($"Зріз d-portal від {today:dd.MM.yyyy}. Звіт збирається командою `report`, числа нижче пораховані з кешу цього збору.");
        md.AppendLine();
        md.AppendLine("## Збір");
        md.AppendLine();
        md.AppendLine($"- активностей з отримувачем Україна у списку d-portal: {N(run.Listed)}");
        md.AppendLine($"- кандидатів для перевірки: {N(run.Candidates)} (за секторами, пошуком слів на d-portal і словами в назвах)");
        md.AppendLine($"- повних записів забрано цього разу: {N(run.Downloaded)}, узято з кешу: {N(run.FromCache)}, не знайдено: {N(run.NotFound)}, з помилкою: {N(run.Failed)}");
        md.AppendLine($"- тривалість збору: {run.Duration:hh\\:mm\\:ss}");
        md.AppendLine();
        md.AppendLine("| Джерело кандидатів | Записів |");
        md.AppendLine("|---|---:|");
        foreach (var (source, count) in run.Sources.OrderByDescending(s => s.Value))
            md.AppendLine($"| {source} | {N(count)} |");
        md.AppendLine();

        md.AppendLine("## Відбір");
        md.AppendLine();
        md.AppendLine($"Відібрано {N(selected.Count)} записів: {N(direct.Count)} прямих і {N(selected.Count - direct.Count)} дотичних.");
        md.AppendLine();
        md.AppendLine("| Головна підстава | Прямі | Дотичні |");
        md.AppendLine("|---|---:|---:|");
        foreach (var basis in Enum.GetValues<Basis>())
            md.AppendLine($"| {BasisName(basis)} | {N(selected.Count(s => s.Selection.Direct && s.Selection.Basis == basis))} | {N(selected.Count(s => !s.Selection.Direct && s.Selection.Basis == basis))} |");
        md.AppendLine();
        md.AppendLine("Найчастіші слова в назвах прямих записів:");
        md.AppendLine();
        var words = selected.Where(s => s.Selection.Direct)
            .SelectMany(s => s.Selection.Reasons.Where(r => r.Basis == Basis.Title).Select(r => r.What))
            .GroupBy(w => w).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(15);
        md.AppendLine(string.Join(", ", words.Select(g => $"{g.Key} ({g.Count()})")));
        md.AppendLine();

        md.AppendLine("## Прямі активності");
        md.AppendLine();
        md.AppendLine($"Без подвійного рахунку програм і їхніх частин: {N(summary.Counted)}. У реалізації {N(summary.Active)}, з них завершуються до {today.AddYears(1):dd.MM.yyyy}: {N(summary.EndingSoon)}. Донорів: {N(summary.Donors.Count)}.");
        md.AppendLine();
        md.AppendLine("| Статус | Записів |");
        md.AppendLine("|---|---:|");
        foreach (var (status, count) in summary.ByStatus)
            md.AppendLine($"| {codelists.Statuses.GetValueOrDefault(status.ToString(), status.ToString())} | {N(count)} |");
        md.AppendLine();

        md.AppendLine("### Донори");
        md.AppendLine();
        md.AppendLine("Суми в доларах США, як їх перераховує d-portal, і тільки за записами, де Україна єдиний отримувач (100%). Це суми за всю активність, а не за її цифрову частину.");
        md.AppendLine();
        md.AppendLine("| Донор | Активностей | У реалізації | Записів зі 100% Україна | Зобов'язання | Виплати |");
        md.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (var d in summary.Donors.Take(25))
            md.AppendLine($"| {Cell(d.Name)} | {N(d.Total)} | {N(d.Active)} | {N(d.WholeCount)} | {Usd(d.WholeCommitment)} | {Usd(d.WholeSpend)} |");
        if (summary.Donors.Count > 25)
            md.AppendLine($"| ще {summary.Donors.Count - 25} донорів | | | | | |");
        md.AppendLine();

        md.AppendLine("### Виконавці");
        md.AppendLine();
        md.AppendLine("Організації з роллю Implementing, які трапляються найчастіше.");
        md.AppendLine();
        md.AppendLine("| Виконавець | Активностей | Донорів |");
        md.AppendLine("|---|---:|---:|");
        foreach (var i in summary.Implementers.Take(20))
            md.AppendLine($"| {Cell(i.Name)} | {N(i.Activities)} | {N(i.Donors.Count)} |");
        md.AppendLine();

        md.AppendLine("### Роки завершення");
        md.AppendLine();
        md.AppendLine("| Рік | Записів |");
        md.AppendLine("|---|---:|");
        foreach (var (year, count) in summary.ByEndYear.Where(y => y.Key >= today.Year - 5))
            md.AppendLine($"| {year} | {N(count)} |");
        md.AppendLine();

        md.AppendLine("## Що видно в самих даних");
        md.AppendLine();
        md.AppendLine("Серед усіх відібраних записів (прямі й дотичні):");
        md.AppendLine();
        var observations = new (string Text, int Count)[]
        {
            ("статус «у реалізації», хоча дата завершення вже минула", all.Count(a => a.Status == Stats.Stats.Implementation && a.End < today)),
            ("Україна не єдиний отримувач (частка менша за 100%)", all.Count(a => a.UkraineShare < 99.995)),
            ("частку України з запису визначити не можна", all.Count(a => a.UkraineShare is null)),
            ("немає жодного виконавця (роль Implementing)", all.Count(a => !a.WithRole(OrgRole.Implementing).Any())),
            ("виконавця вказано тільки типом організації, без назви", all.Count(a => a.WithRole(OrgRole.Implementing).Any(Organisations.IsGeneric))),
            ("назву виконавця приховано донором або вона схожа на ім'я людини", all.Count(a => a.WithRole(OrgRole.Implementing).Any(p => p.Name is null))),
            ("назву активності приховано донором", all.Count(a => ActivityParser.IsRedacted(a.Title))),
            ("немає зобов'язань (сума 0 або порожня)", all.Count(a => (a.Commitment ?? 0) == 0)),
            ("немає секторів DAC ні в активності, ні в транзакціях", all.Count(a => a.Sectors.Count == 0)),
            ("немає дати завершення", all.Count(a => a.End is null)),
            ("програма, частини якої теж є в переліку", Stats.Stats.Parents(all).Count),
        };
        foreach (var (text, count) in observations)
            md.AppendLine($"- {text}: {N(count)}");
        md.AppendLine();

        md.AppendLine("## Завершуються протягом року");
        md.AppendLine();
        md.AppendLine("Прямі активності у статусі «у реалізації» з датою завершення в найближчі 12 місяців.");
        md.AppendLine();
        md.AppendLine("| Завершення | Донор | Активність |");
        md.AppendLine("|---|---|---|");
        var parents = Stats.Stats.Parents(direct);
        foreach (var a in direct.Where(a => !parents.Contains(a.Id) && Stats.Stats.IsEndingSoon(a, today)).OrderBy(a => a.End).ThenBy(a => a.Id))
            md.AppendLine($"| {a.End:dd.MM.yyyy} | {Cell(a.Publisher.Name ?? "")} | [{Cell(a.Title)}](https://d-portal.org/ctrack.html#view=act&aid={Uri.EscapeDataString(a.Id)}) |");

        return md.ToString();
    }

    private static string BasisName(Basis basis) => basis switch
    {
        Basis.Sector => "сектор ІКТ",
        Basis.Title => "слово в назві",
        _ => "слово лише в описі"
    };

    private static string N(int value) => value.ToString("N0", Uk);

    private static string Usd(decimal value) => Math.Abs(value) switch
    {
        0 => "0",
        < 50_000m => "$" + Math.Round(value / 1_000m).ToString("N0", Uk) + " тис.",
        _ => "$" + Math.Round(value / 1_000_000m, 1).ToString("N1", Uk) + " млн"
    };

    private static string Cell(string text) => text.Replace("|", "/").Replace("\n", " ").Trim();
}
