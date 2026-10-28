using Halyard.Models.Downloads;
using Halyard.Models.Library;
using Halyard.ViewModels;

namespace Halyard.UI;

public interface IDialogService
{
    /// <param name="existingFolder">Import mode: verify this folder instead of installing from scratch.</param>
    /// <returns>The install the user configured, or <see langword="null"/> if they canceled.</returns>
    Task<InstallRequest?> ShowInstallAsync(Game game, string? existingFolder = null);

    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    /// <returns>The edited options, or <see langword="null"/> if the user canceled.</returns>
    Task<LaunchOptions?> EditLaunchOptionsAsync(Game game);

    /// <summary>Live statistics of one download, refreshed while the dialog is open.</summary>
    Task ShowDownloadDetailsAsync(DownloadItemViewModel download);
}

public sealed record LaunchOptions(string? CustomArguments, bool LaunchOffline);
