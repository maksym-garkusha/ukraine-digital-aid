using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace UkraineDigitalAid.Iati;

/// <summary>
/// Клієнт до d-portal.org. Сервіс волонтерський, тому запити йдуть з паузою,
/// а збої мережі й відповіді 429/5xx повторюються кілька разів зі зростаючою затримкою.
/// </summary>
public class DPortalClient(HttpClient http, TimeSpan pause, TimeSpan retryDelay)
{
    public const string ActivityRoot = "/iati-activities/iati-activity";
    private const int Attempts = 4;

    public static DPortalClient Create(HttpClient http) =>
        new(http, TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(3));

    /// <summary>
    /// Читає список сторінками через limit/offset. Без orderby d-portal не гарантує
    /// порядок рядків, і сторінки могли б перекриватися.
    /// </summary>
    public async IAsyncEnumerable<JsonElement> ListAsync(
        IReadOnlyList<(string Key, string Value)> filter, string select, int pageSize,
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (int offset = 0; ; offset += pageSize)
        {
            var query = filter.Concat([
                ("select", select), ("orderby", "aid"),
                ("limit", pageSize.ToString(CultureInfo.InvariantCulture)), ("offset", offset.ToString(CultureInfo.InvariantCulture))]).ToList();
            using var page = await QueryAsync(query, ct);
            var rows = page.RootElement.GetProperty("rows");
            foreach (var row in rows.EnumerateArray())
                yield return row.Clone();
            if (rows.GetArrayLength() < pageSize)
                yield break;
        }
    }

    /// <summary>
    /// Повний запис активності: JSON, у якому ключі це шляхи XML IATI.
    /// null, якщо такого ідентифікатора d-portal не знає.
    /// </summary>
    public async Task<JsonElement?> GetActivityAsync(string aid, CancellationToken ct)
    {
        using var result = await QueryAsync([("from", "xson"), ("root", ActivityRoot), ("aid", aid)], ct);
        foreach (var item in result.RootElement.GetProperty("xson").EnumerateArray())
            if (item.TryGetProperty(ActivityRoot, out var activities) && activities.GetArrayLength() > 0)
                return activities[0].Clone();
        return null;
    }

    public async Task<JsonDocument> QueryAsync(IEnumerable<(string Key, string Value)> query, CancellationToken ct)
    {
        string url = BuildUrl(query);
        for (int attempt = 1; ; attempt++)
        {
            await Task.Delay(pause, ct);
            try
            {
                // тіло читається цілком ще всередині GetAsync: так HttpClient.Timeout покриває і його,
                // а MaxResponseContentBufferSize обмежує розмір
                using var response = await http.GetAsync(url, ct);
                if (IsTransient(response.StatusCode) && attempt < Attempts)
                {
                    await Task.Delay(RetryAfter(response) ?? Backoff(attempt), ct);
                    continue;
                }
                response.EnsureSuccessStatusCode();
                await using var body = await response.Content.ReadAsStreamAsync(ct);
                return await JsonDocument.ParseAsync(body, cancellationToken: ct);
            }
            catch (Exception e) when (attempt < Attempts && !ct.IsCancellationRequested
                && e is HttpRequestException { StatusCode: null } or TaskCanceledException or IOException)
            {
                // обрив з'єднання або тайм-аут HttpClient; відповіді з кодом сюди не потрапляють
                await Task.Delay(Backoff(attempt), ct);
            }
        }
    }

    public static string BuildUrl(IEnumerable<(string Key, string Value)> query)
    {
        var url = new StringBuilder("q?");
        foreach (var (key, value) in query.Append(("form", "json")))
            url.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value)).Append('&');
        return url.ToString(0, url.Length - 1);
    }

    private static bool IsTransient(HttpStatusCode code) =>
        code == HttpStatusCode.TooManyRequests || (int)code >= 500;

    private TimeSpan Backoff(int attempt) => retryDelay * Math.Pow(2, attempt - 1);

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var delta = response.Headers.RetryAfter?.Delta;
        return delta is { } d && d < TimeSpan.FromMinutes(2) ? d : null;
    }
}
