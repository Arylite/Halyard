using Halyard.Models.Installs;
using Halyard.Models.Library;

namespace Halyard.Core.Installs;

/// <summary>A downloaded manifest, its raw bytes (kept for later updates) and where its chunks live.</summary>
public sealed record RemoteManifest(BuildManifest Manifest, byte[] RawData, IReadOnlyList<Uri> ChunkBaseUris);

/// <summary>Fetches the latest build manifest of a game from Epic.</summary>
public interface IManifestSource
{
    Task<RemoteManifest> GetLatestAsync(Game game, CancellationToken cancellationToken);
}
