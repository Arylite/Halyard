using Halyard.Models.Downloads;

namespace Halyard.Core.Installs;

/// <summary>Installs, updates or repairs a game. Interrupted runs resume where they stopped.</summary>
public interface IGameInstaller
{
    Task RunAsync(InstallRequest request, IProgress<DownloadProgress> progress, CancellationToken cancellationToken);
}
