using Halyard.Core.Installs;
using Halyard.Core.Library;
using Halyard.Core.Settings;
using Halyard.Models.Downloads;
using Halyard.Models.Installs;
using Halyard.Models.Library;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Installs;

public sealed class GameInstaller(
    ILibraryService library,
    IManifestSource manifests,
    IInstalledManifestStore installedManifests,
    InstallPipeline pipeline,
    ISettingsService settings,
    TimeProvider time,
    ILogger<GameInstaller> logger) : IGameInstaller
{
    /// <summary>Epic chunks hold 1 MiB once decompressed.</summary>
    private const int ChunkMegabytes = 1;

    public async Task RunAsync(InstallRequest request, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        await using var tracker = new InstallProgressTracker(progress, time);
        tracker.StartPhase(InstallPhase.Preparing, 0);

        var game = library.Find(request.GameId) ?? throw new InvalidOperationException($"{request.DisplayName} is no longer in the library.");
        var remote = await manifests.GetLatestAsync(game, cancellationToken);
        var manifest = remote.Manifest;
        var tags = request.InstallTags.ToHashSet(StringComparer.Ordinal);
        var selected = manifest.Files.Where(file => file.IsSelectedBy(tags)).ToList();

        Directory.CreateDirectory(request.InstallDirectory);
        await using var journal = await InstallJournal.OpenAsync(
            request.InstallDirectory, manifest.BuildVersion, resume: request.Kind != InstallKind.Repair, cancellationToken);

        var previous = request.Kind == InstallKind.Update
            ? await installedManifests.LoadAsync(request.InstallDirectory, cancellationToken)
            : null;

        var toWrite = request.Kind switch
        {
            InstallKind.Install => Pending(selected, journal, request.InstallDirectory),
            InstallKind.Update when previous is not null => Pending(Changed(selected, previous), journal, request.InstallDirectory),

            // Repairs, and updates without a known previous build, compare against what's on disk.
            _ => await FileVerifier.FindDamagedAsync(request.InstallDirectory, Pending(selected, journal, request.InstallDirectory), tracker, cancellationToken),
        };

        logger.LogInformation(
            "{Kind} {Game} {Version}: {Count} of {Total} file(s) to write",
            request.Kind, game.Title, manifest.BuildVersion, toWrite.Count, selected.Count);

        if (toWrite.Count > 0)
        {
            await DownloadAsync(toWrite, remote, request.InstallDirectory, journal, tracker, cancellationToken);
        }

        tracker.StartPhase(InstallPhase.Finalizing, 0);
        if (previous is not null)
        {
            DeleteObsoleteFiles(previous, selected, request);
        }

        await installedManifests.SaveAsync(request.InstallDirectory, remote.RawData, cancellationToken);
        await journal.CompleteAsync();

        await library.UpdateAsync(game.Id, current => current with
        {
            LatestVersion = manifest.BuildVersion,
            Installation = new GameInstallation
            {
                InstallDirectory = request.InstallDirectory,
                Executable = manifest.LaunchExecutable,
                Arguments = manifest.LaunchCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                Version = manifest.BuildVersion,
                InstallTags = request.InstallTags,
                InstallSize = selected.Sum(file => file.Size),
                Source = InstallationSource.Halyard,
                Prerequisite = manifest.Prerequisite,
            },
        }, cancellationToken);
    }

    private async Task DownloadAsync(
        IReadOnlyList<ManifestFile> files,
        RemoteManifest remote,
        string installDirectory,
        InstallJournal journal,
        InstallProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        // Rewritten files replace what is on disk: only the growth needs free space.
        EnsureFreeSpace(installDirectory, files.Sum(file => Math.Max(0, file.Size - Math.Max(0, ExistingSize(installDirectory, file)))));

        var current = settings.Current;
        var maxBufferedChunks = Math.Max(64, current.ChunkCacheMegabytes / ChunkMegabytes);
        var workers = Math.Clamp(current.DownloadWorkers, 1, 64);

        var plan = InstallPlanner.Build(files, remote.Manifest, reuseWindow: maxBufferedChunks / 2);
        logger.LogInformation(
            "Downloading {Download:N0} bytes to write {Write:N0} bytes ({Chunks} chunk downloads, {Workers} workers, {Cdns} CDN(s), multi-CDN {MultiCdn})",
            plan.DownloadBytes, plan.WriteBytes, plan.Tasks.Count, workers, remote.ChunkBaseUris.Count, current.ExperimentalMultiCdn);

        tracker.StartPhase(InstallPhase.Downloading, plan.DownloadBytes);
        await pipeline.RunAsync(
            plan,
            remote.Manifest,
            installDirectory,
            remote.ChunkBaseUris,
            workers,
            maxBufferedChunks,
            () => settings.Current.ExperimentalMultiCdn,
            journal,
            tracker,
            cancellationToken);
    }

    /// <summary>Skips files a previous, interrupted run already finished.</summary>
    private static List<ManifestFile> Pending(IEnumerable<ManifestFile> files, InstallJournal journal, string installDirectory) =>
        [.. files.Where(file => !(journal.IsCompleted(file.Path) && SizeMatches(installDirectory, file)))];

    private static IEnumerable<ManifestFile> Changed(IEnumerable<ManifestFile> files, BuildManifest previous)
    {
        var previousHashes = previous.Files.ToDictionary(file => file.Path, file => file.Sha1, StringComparer.OrdinalIgnoreCase);
        return files.Where(file => !previousHashes.TryGetValue(file.Path, out var sha1) || !sha1.AsSpan().SequenceEqual(file.Sha1));
    }

    private void DeleteObsoleteFiles(BuildManifest previous, IReadOnlyList<ManifestFile> current, InstallRequest request)
    {
        var kept = current.Select(file => file.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in previous.Files.Where(file => !kept.Contains(file.Path)))
        {
            var path = Path.Combine(request.InstallDirectory, file.Path);
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not delete obsolete file {Path}", path);
            }
        }
    }

    private static bool SizeMatches(string installDirectory, ManifestFile file) =>
        ExistingSize(installDirectory, file) == file.Size;

    private static long ExistingSize(string installDirectory, ManifestFile file)
    {
        var info = new FileInfo(Path.Combine(installDirectory, file.Path));
        return info.Exists ? info.Length : -1;
    }

    private static void EnsureFreeSpace(string installDirectory, long requiredBytes)
    {
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(installDirectory))!);
        if (drive.AvailableFreeSpace < requiredBytes)
        {
            throw new IOException(
                $"Not enough space on {drive.Name}: {requiredBytes / (double)(1L << 30):N1} GB needed, {drive.AvailableFreeSpace / (double)(1L << 30):N1} GB free.");
        }
    }
}
