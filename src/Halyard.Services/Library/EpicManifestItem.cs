using System.Text.Json.Serialization;

namespace Halyard.Services.Library;

/// <summary>The subset of an Epic Games Launcher <c>.item</c> install manifest that Halyard reads.</summary>
internal sealed class EpicManifestItem
{
    public string AppName { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string InstallLocation { get; init; } = "";

    public string LaunchExecutable { get; init; } = "";

    public string? LaunchCommand { get; init; }

    public string? AppVersionString { get; init; }

    public string? CatalogNamespace { get; init; }

    public string? CatalogItemId { get; init; }

    public long InstallSize { get; init; }

    public List<string> InstallTags { get; init; } = [];

    [JsonPropertyName("bIsIncompleteInstall")]
    public bool IsIncompleteInstall { get; init; }
}
