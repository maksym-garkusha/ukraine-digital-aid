using System.Net;
using System.Text;

namespace UkraineDigitalAid.Tests;

/// <summary>Підставний HTTP: відповідає за функцією і запам'ятовує запити.</summary>
public class FakeHandler(Func<Uri, int, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    public static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public HttpClient Client() => new(this) { BaseAddress = new Uri("https://d-portal.test/") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(respond(request.RequestUri!, Requests.Count));
    }
}
