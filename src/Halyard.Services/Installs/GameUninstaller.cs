using Halyard.Core.Installs;
using Halyard.Core.Library;
using Halyard.Core.Settings;
using Halyard.Models.Library;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Installs;

public sealed class GameUninstaller(ILibraryService library, ISettingsService settings, ILogger<GameUninstaller> logger) : IGameUninstaller
{
    public async Task UninstallAsync(string gameId, CancellationToken cancellationToken)
    {
        if (library.Find(gameId) is not { Installation: { } installation } game)
        {
            return;
        }

        switch (installation.Source)
        {
            case InstallationSource.Halyard:
                EnsureSafeToDelete(installation.InstallDirectory);
                logger.LogInformation("Uninstalling {Game} from {Directory}", game.Title, installation.InstallDirectory);
                await Task.Run(() => DeleteDirectory(installation.InstallDirectory), cancellationToken);
                await library.UpdateAsync(gameId, current => current with { Installation = null }, cancellationToken);
                break;

            case InstallationSource.Manual:
                // The files belong to the user; only forget the shortcut.
                await library.RemoveAsync(gameId, cancellationToken);
                break;

            case InstallationSource.EpicLauncher:
                throw new InvalidOperationException($"{game.Title} was installed by the Epic Games Launcher; uninstall it there.");
        }
    }

    /// <summary>
    /// The install folder is deleted recursively, so refuse folders that obviously hold more than one
    /// game: a drive root, the games folder itself, or a user or system folder (an install or import
    /// pointed straight at one of them).
    /// </summary>
    private void EnsureSafeToDelete(string directory)
    {
        var path = Normalize(directory);
        string[] protectedFolders =
        [
            settings.Current.InstallDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        ];

        var isRoot = Path.GetPathRoot(path) is { } root && string.Equals(Normalize(root), path, StringComparison.OrdinalIgnoreCase);
        if (isRoot || protectedFolders.Any(folder => folder.Length > 0 && string.Equals(Normalize(folder), path, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"{directory} is not a game's own folder, so Halyard won't delete it. Remove the game's files by hand.");
        }
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static void DeleteDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        // Read-only files would make the recursive delete fail.
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(directory, recursive: true);
    }
}
