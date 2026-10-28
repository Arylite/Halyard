using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Halyard.Core.Diagnostics;
using Halyard.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Diagnostics;

/// <summary>
/// Logs, configuration and a system summary. Allow-list only: the session file (tokens) and the
/// sign-in browser profile (cookies) can never end up in the archive.
/// </summary>
public sealed class DiagnosticsExporter(AppPaths paths, TimeProvider time, ILogger<DiagnosticsExporter> logger) : IDiagnosticsExporter
{
    public async Task ExportAsync(string zipPath, CancellationToken cancellationToken)
    {
        await using var output = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var zip = new ZipArchive(output, ZipArchiveMode.Create);

        foreach (var file in new[] { paths.SettingsFile, paths.LibraryFile, paths.QueueFile, paths.PrerequisitesFile })
        {
            await AddFileAsync(zip, file, Path.GetFileName(file), cancellationToken);
        }

        if (Directory.Exists(paths.LogsDirectory))
        {
            foreach (var log in Directory.EnumerateFiles(paths.LogsDirectory, "*.log"))
            {
                await AddFileAsync(zip, log, $"logs/{Path.GetFileName(log)}", cancellationToken);
            }
        }

        var system = zip.CreateEntry("system.txt");
        await using (var writer = new StreamWriter(await system.OpenAsync(cancellationToken)))
        {
            await writer.WriteAsync(DescribeSystem());
        }

        logger.LogInformation("Diagnostics exported to {Path}", zipPath);
    }

    private static async Task AddFileAsync(ZipArchive zip, string path, string entryName, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return;
        }

        // The current log file is open for writing: share it rather than fail.
        await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        await using var target = await zip.CreateEntry(entryName, CompressionLevel.Optimal).OpenAsync(cancellationToken);
        await source.CopyToAsync(target, cancellationToken);
    }

    private string DescribeSystem() => string.Join(Environment.NewLine,
        $"Exported: {time.GetLocalNow():u}",
        $"Halyard: {Assembly.GetExecutingAssembly().GetName().Version}",
        $"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
        $".NET: {RuntimeInformation.FrameworkDescription}",
        $"CPU cores: {Environment.ProcessorCount}",
        $"Memory: {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1L << 20):N0} MB available to the process",
        string.Empty);
}
