using Halyard.Models.Settings;

namespace Halyard.Core.Settings;

public interface ISettingsService
{
    LauncherSettings Current { get; }

    event EventHandler<LauncherSettings>? Changed;

    Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default);
}
