namespace Halyard.Services.Installs;

/// <summary>The CDN one download worker last used; only that worker reads and writes it.</summary>
internal sealed class CdnAffinity
{
    public int Cdn { get; set; } = -1;
}
