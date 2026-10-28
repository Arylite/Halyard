namespace Halyard.Models.Installs;

/// <summary>A piece of optional content the user can choose to install.</summary>
/// <param name="ExtraInstallSize">Disk space it adds on top of the required content.</param>
/// <param name="ExtraDownloadSize">Download it adds on top of the required content.</param>
/// <param name="IsRecommended">Selected by default.</param>
public sealed record InstallOption(string Tag, long ExtraInstallSize, long ExtraDownloadSize, bool IsRecommended);
