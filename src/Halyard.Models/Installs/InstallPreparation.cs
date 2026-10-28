namespace Halyard.Models.Installs;

/// <summary>Everything the install dialog needs: the build and its optional content.</summary>
public sealed record InstallPreparation(
    BuildManifest Manifest,
    InstallSize RequiredSize,
    IReadOnlyList<InstallOption> Options);
