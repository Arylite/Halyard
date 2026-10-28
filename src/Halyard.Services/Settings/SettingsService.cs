using System.Text.Json;
using Halyard.Core.Hosting;
using Halyard.Core.Serialization;
using Halyard.Core.Settings;
using Halyard.Core.Storage;
using Halyard.Models.Settings;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Settings;

public sealed class SettingsService(AppPaths paths, ILogger<SettingsService> logger) : ISettingsService, IAsyncInitializer
{
    private readonly JsonFileStore<LauncherSettings> _store =
        new(paths.SettingsFile, HalyardJsonContext.Default.LauncherSettings);

    public LauncherSettings Current { get; private set; } = new();

    public event EventHandler<LauncherSettings>? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            Current = await _store.ReadAsync(cancellationToken) ?? new LauncherSettings();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Settings file is unreadable, using defaults");
        }
    }

    public async Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
    {
        await _store.WriteAsync(settings, cancellationToken);
        Current = settings;
        logger.LogInformation("Settings saved");
        Changed?.Invoke(this, settings);
    }
}
