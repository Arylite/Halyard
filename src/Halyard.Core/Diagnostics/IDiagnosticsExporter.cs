namespace Halyard.Core.Diagnostics;

/// <summary>Bundles what a bug report needs into one zip. Never includes tokens or browser data.</summary>
public interface IDiagnosticsExporter
{
    Task ExportAsync(string zipPath, CancellationToken cancellationToken);
}
