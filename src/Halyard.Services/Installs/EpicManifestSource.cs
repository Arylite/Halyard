using Halyard.Core.Epic;
using Halyard.Core.Installs;
using Halyard.Models.Library;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Installs;

/// <summary>
/// Downloads build manifests from Epic's CDNs. The last one is cached, so preparing an install and
/// then running it costs a single (often large) manifest download.
/// </summary>
public sealed class EpicManifestSource(
    IEpicLauncherApi api,
    IManifestParser parser,
    HttpClient http,
    ILogger<EpicManifestSource> logger) : IManifestSource
{
    // A reference, not a tuple: concurrent installs read it while another writes it, and a torn
    // struct could pair one game's key with another game's manifest.
    private volatile CachedManifest? _cache;

    public async Task<RemoteManifest> GetLatestAsync(Game game, CancellationToken cancellationToken)
    {
        if (!game.IsDownloadable)
        {
            throw new InvalidOperationException($"{game.Title} can't be downloaded from Epic.");
        }

        var location = await api.GetManifestLocationAsync(
            game.EpicNamespace!, game.EpicCatalogItemId!, game.EpicAppName!, cancellationToken);

        var key = $"{game.EpicAppName}@{location.BuildVersion}";
        if (_cache is { } cached && cached.Key == key)
        {
            return cached.Manifest;
        }

        Exception? lastError = null;
        foreach (var uri in location.ManifestUris)
        {
            try
            {
                var raw = await http.GetByteArrayAsync(uri, cancellationToken);
                var manifest = await Task.Run(() => parser.Parse(raw), cancellationToken);

                // The CDN that just answered goes first for chunks too.
                IReadOnlyList<Uri> chunkBases = [.. location.ManifestUris.Prepend(uri).Select(GetDirectory).Distinct()];
                var remote = new RemoteManifest(manifest, raw, chunkBases);

                _cache = new CachedManifest(key, remote);
                logger.LogInformation(
                    "Manifest for {Game} {Version}: {Files} files, {Chunks} chunks",
                    game.Title, manifest.BuildVersion, manifest.Files.Count, manifest.Chunks.Count);
                return remote;
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidDataException ||
                (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // A timeout or a corrupt copy on one CDN: the next one may serve it fine.
                lastError = ex;
                logger.LogWarning(ex, "Manifest download failed from {Host}", uri.Host);
            }
        }

        throw new HttpRequestException($"No CDN could serve the manifest of {game.Title}.", lastError);
    }

    private sealed record CachedManifest(string Key, RemoteManifest Manifest);

    private static Uri GetDirectory(Uri manifestUri)
    {
        var path = manifestUri.GetLeftPart(UriPartial.Path);
        return new Uri(path[..(path.LastIndexOf('/') + 1)]);
    }
}
