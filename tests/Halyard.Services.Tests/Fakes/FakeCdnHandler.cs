using System.Collections.Concurrent;
using System.Net;

namespace Halyard.Services.Tests.Fakes;

/// <summary>
/// Serves chunk files from memory under any of <paramref name="baseUris"/>. Can fail the first
/// request of every file, and make one host slow, to exercise retries and hedging.
/// </summary>
internal sealed class FakeCdnHandler(
    IReadOnlyList<Uri> baseUris,
    IReadOnlyDictionary<string, byte[]> files,
    bool failFirstAttempt = false,
    string? slowHost = null) : HttpMessageHandler
{
    private static readonly TimeSpan SlowDelay = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, int> _attempts = new();
    private readonly ConcurrentDictionary<string, int> _requestsPerHost = new();

    public int RequestCount => _attempts.Values.Sum();

    public IReadOnlyDictionary<string, int> RequestsPerHost => _requestsPerHost;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        _requestsPerHost.AddOrUpdate(uri.Host, 1, (_, count) => count + 1);

        var baseUri = baseUris.First(candidate => candidate.Host == uri.Host);
        var path = baseUri.MakeRelativeUri(uri).OriginalString;
        var attempt = _attempts.AddOrUpdate(path, 1, (_, count) => count + 1);

        if (uri.Host == slowHost)
        {
            await Task.Delay(SlowDelay, cancellationToken);
        }

        if (failFirstAttempt && attempt == 1)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }

        return files.TryGetValue(path, out var body)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
