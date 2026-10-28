using System.ComponentModel;
using System.Diagnostics;
using Halyard.Core.Installs;
using Halyard.Core.Serialization;
using Halyard.Core.Storage;
using Halyard.Models.Library;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Installs;

/// <summary>
/// Mirrors the official launcher: a build's prerequisite installer runs elevated (Windows asks for
/// consent) the first time the game starts, and its ids are remembered so it never runs twice.
/// </summary>
public sealed class PrerequisiteInstaller(
    IInstalledManifestStore installedManifests,
    AppPaths paths,
    ILogger<PrerequisiteInstaller> logger) : IPrerequisiteInstaller, IDisposable
{
    private const int ErrorCancelled = 1223;

    /// <summary>Installer exit codes that mean success: done, reboot required, reboot started.</summary>
    private static readonly int[] SuccessCodes = [0, 1641, 3010];

    private readonly JsonFileStore<List<string>> _store = new(paths.PrerequisitesFile, HalyardJsonContext.Default.ListString);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task EnsureInstalledAsync(Game game, CancellationToken cancellationToken)
    {
        if (game.Installation is not { Source: InstallationSource.Halyard } installation)
        {
            // The official launcher already ran them; manual games have none.
            return;
        }

        // Installs made before prerequisites were tracked: the stored manifest still knows.
        var prerequisite = installation.Prerequisite
            ?? (await installedManifests.LoadAsync(installation.InstallDirectory, cancellationToken))?.Prerequisite;
        if (prerequisite is null)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var done = (await _store.ReadAsync(cancellationToken) ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var ids = Keys(game, prerequisite);
            if (ids.All(done.Contains))
            {
                return;
            }

            var executable = Path.Combine(installation.InstallDirectory, prerequisite.Path);
            if (!File.Exists(executable))
            {
                logger.LogWarning("Prerequisite {Name} of {Game} is missing at {Path}; launching anyway", prerequisite.Name, game.Title, executable);
                return;
            }

            await RunElevatedAsync(game, prerequisite, executable, cancellationToken);

            done.UnionWith(ids);
            await _store.WriteAsync([.. done], cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RunElevatedAsync(Game game, GamePrerequisite prerequisite, string executable, CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(prerequisite.Name) ? "required components" : prerequisite.Name;
        logger.LogInformation("Installing {Name} for {Game}", name, game.Title);

        Process? process;
        try
        {
            process = Process.Start(new ProcessStartInfo(executable, prerequisite.Arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(executable),
            });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            throw new InvalidOperationException(
                $"{game.Title} needs {name} installed first. Accept the administrator prompt to continue.", ex);
        }

        using (process)
        {
            if (process is null)
            {
                return;
            }

            await process.WaitForExitAsync(cancellationToken);
            if (!SuccessCodes.Contains(process.ExitCode))
            {
                throw new InvalidOperationException($"Installing {name} failed (exit code {process.ExitCode}).");
            }
        }

        logger.LogInformation("{Name} installed for {Game}", name, game.Title);
    }

    /// <summary>A prerequisite without ids is tracked per game, by its installer path.</summary>
    private static IReadOnlyList<string> Keys(Game game, GamePrerequisite prerequisite) =>
        prerequisite.Ids.Count > 0 ? prerequisite.Ids : [$"{game.Id}|{prerequisite.Path}"];

    public void Dispose() => _gate.Dispose();
}
