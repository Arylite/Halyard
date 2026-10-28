using Halyard.Core.Storage;

namespace Halyard.Services.Installs;

/// <summary>
/// Remembers which files of a build are fully written, so an interrupted install or update
/// resumes instead of starting over. Tied to a build version: a new build starts a new journal.
/// </summary>
internal sealed class InstallJournal : IAsyncDisposable
{
    private readonly string _path;
    private readonly HashSet<string> _completed;
    private readonly StreamWriter _writer;

    private InstallJournal(string path, HashSet<string> completed, StreamWriter writer)
    {
        _path = path;
        _completed = completed;
        _writer = writer;
    }

    public static async Task<InstallJournal> OpenAsync(
        string installDirectory,
        string buildVersion,
        bool resume,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppPaths.InstallMetadataDirectory(installDirectory), "journal.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        HashSet<string> completed = new(StringComparer.OrdinalIgnoreCase);
        if (resume && File.Exists(path))
        {
            var lines = await File.ReadAllLinesAsync(path, cancellationToken);
            if (lines.Length > 0 && lines[0] == buildVersion)
            {
                completed.UnionWith(lines.Skip(1));
            }
        }

        var writer = new StreamWriter(path, append: false) { AutoFlush = false };
        await writer.WriteLineAsync(buildVersion.AsMemory(), cancellationToken);
        foreach (var file in completed)
        {
            await writer.WriteLineAsync(file.AsMemory(), cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);
        return new InstallJournal(path, completed, writer);
    }

    public bool IsCompleted(string relativePath) => _completed.Contains(relativePath);

    public async Task MarkCompletedAsync(string relativePath, CancellationToken cancellationToken)
    {
        _completed.Add(relativePath);
        await _writer.WriteLineAsync(relativePath.AsMemory(), cancellationToken);
        await _writer.FlushAsync(cancellationToken);
    }

    /// <summary>Called once the build is fully installed: nothing is left to resume.</summary>
    public async Task CompleteAsync()
    {
        await _writer.DisposeAsync();
        File.Delete(_path);
    }

    public ValueTask DisposeAsync() => _writer.DisposeAsync();
}
