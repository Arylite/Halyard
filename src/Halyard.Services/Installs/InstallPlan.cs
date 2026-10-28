using Halyard.Models.Installs;

namespace Halyard.Services.Installs;

/// <summary>One download of a chunk, shared by every part that uses it while it stays in memory.</summary>
internal sealed class ChunkTask(ManifestChunk chunk)
{
    public ManifestChunk Chunk => chunk;

    /// <summary>Parts still to be written from this download; at zero the buffer is released.</summary>
    public int RemainingUses;

    public TaskCompletionSource<PooledChunk> Data { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal readonly record struct PlannedPart(ChunkTask Task, int Offset, int Size);

internal sealed record PlannedFile(ManifestFile File, IReadOnlyList<PlannedPart> Parts);

/// <summary>Files to write in order, and the chunk downloads that feed them in the order needed.</summary>
internal sealed record InstallPlan(IReadOnlyList<PlannedFile> Files, IReadOnlyList<ChunkTask> Tasks)
{
    public long DownloadBytes { get; } = Tasks.Sum(task => task.Chunk.FileSize);

    public long WriteBytes { get; } = Files.Sum(file => file.File.Size);
}
