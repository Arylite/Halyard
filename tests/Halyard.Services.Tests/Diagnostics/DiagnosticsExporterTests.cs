using System.IO.Compression;
using Halyard.Core.Storage;
using Halyard.Services.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Halyard.Services.Tests.Diagnostics;

public sealed class DiagnosticsExporterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"halyard-diag-{Guid.NewGuid():N}");

    [Fact]
    public async Task Export_IncludesLogsAndConfigButNeverSecrets()
    {
        var paths = new AppPaths(_root);
        Directory.CreateDirectory(paths.LogsDirectory);
        Directory.CreateDirectory(paths.WebViewDataDirectory);
        await File.WriteAllTextAsync(paths.SettingsFile, "{}", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(paths.SessionFile, "tokens", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(paths.LogsDirectory, "halyard-20261027.log"), "log", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(paths.WebViewDataDirectory, "Cookies"), "cookies", TestContext.Current.CancellationToken);

        var zipPath = Path.Combine(_root, "report.zip");
        await new DiagnosticsExporter(paths, TimeProvider.System, NullLogger<DiagnosticsExporter>.Instance)
            .ExportAsync(zipPath, TestContext.Current.CancellationToken);

        using var zip = ZipFile.OpenRead(zipPath);
        var entries = zip.Entries.Select(entry => entry.FullName).ToList();
        Assert.Contains("settings.json", entries);
        Assert.Contains("logs/halyard-20261027.log", entries);
        Assert.Contains("system.txt", entries);
        Assert.DoesNotContain(entries, entry => entry.Contains("session", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, entry => entry.Contains("Cookies", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
