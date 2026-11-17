using System.Globalization;
using System.Text.Json.Serialization;

namespace UkraineDigitalAid.Iati;

/// <summary>
/// Рядок з таблиці act у d-portal. Суми d-portal сам перераховує в долари США,
/// тому беремо їх звідси, а не з транзакцій у повному записі.
/// </summary>
public record ListRow(
    [property: JsonPropertyName("aid")] string Aid,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("reporting")] string? Reporting,
    [property: JsonPropertyName("reporting_ref")] string? ReportingRef,
    [property: JsonPropertyName("status_code")] int? StatusCode,
    [property: JsonPropertyName("day_start")] int? DayStart,
    [property: JsonPropertyName("day_end")] int? DayEnd,
    [property: JsonPropertyName("commitment")] decimal? Commitment,
    [property: JsonPropertyName("spend")] decimal? Spend,
    [property: JsonPropertyName("country_percent")] double? CountryPercent)
{
    public const string Columns =
        "aid,title,reporting,reporting_ref,status_code,day_start,day_end,commitment,spend,country_percent";

    public DateOnly? Start => Days.ToDate(DayStart);

    public DateOnly? End => Days.ToDate(DayEnd);

    /// <summary>
    /// Відбиток полів, які змінюються, коли донор оновлює запис. За ним кеш
    /// вирішує, чи треба забирати повний запис наново.
    /// </summary>
    public string Fingerprint() => string.Create(CultureInfo.InvariantCulture,
        $"{Title}|{StatusCode}|{DayStart}|{DayEnd}|{Commitment}|{Spend}|{CountryPercent}");
}

public static class Days
{
    // d-portal зберігає дати як кількість днів від 1970-01-01
    private static readonly DateOnly Epoch = new(1970, 1, 1);

    public static DateOnly? ToDate(int? days) => days is { } d ? Epoch.AddDays(d) : null;
}
