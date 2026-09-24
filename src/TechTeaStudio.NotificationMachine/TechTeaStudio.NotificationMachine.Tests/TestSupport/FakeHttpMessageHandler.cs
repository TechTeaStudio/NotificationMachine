using System.Net;

namespace TechTeaStudio.NotificationMachine.Tests.TestSupport;

internal sealed record CapturedRequest(HttpRequestMessage Request, string? Body);

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Handler { get; set; }

    public List<CapturedRequest> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(request, body));

        if (Handler is not null)
            return await Handler(request, cancellationToken);

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") };
    }
}
