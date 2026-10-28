using Halyard.Models.Installs;

namespace Halyard.Services.Installs;

/// <summary>
/// Orders chunk downloads by first use. A chunk used again within <c>reuseWindow</c> parts stays in
/// memory and is shared; one reused later is downloaded again instead. That bounds memory to about
/// the window, whatever the build, at the cost of rare duplicate downloads.
/// </summary>
internal static class InstallPlanner
{
    public static InstallPlan Build(IEnumerable<ManifestFile> files, BuildManifest manifest, int reuseWindow)
    {
        List<ChunkTask> tasks = [];
        List<PlannedFile> plannedFiles = [];
        var lastUse = new Dictionary<ChunkGuid, (ChunkTask Task, long Step)>();
        long step = 0;

        foreach (var file in files)
        {
            var parts = new PlannedPart[file.Parts.Count];
            for (var i = 0; i < parts.Length; i++, step++)
            {
                var part = file.Parts[i];
                if (!lastUse.TryGetValue(part.Chunk, out var use) || step - use.Step > reuseWindow)
                {
                    var chunk = manifest.Chunks.GetValueOrDefault(part.Chunk)
                        ?? throw new InvalidDataException($"{file.Path} references unknown chunk {part.Chunk}.");
                    use = (new ChunkTask(chunk), step);
                    tasks.Add(use.Task);
                }

                use.Task.RemainingUses++;
                lastUse[part.Chunk] = (use.Task, step);
                parts[i] = new PlannedPart(use.Task, (int)part.Offset, (int)part.Size);
            }

            plannedFiles.Add(new PlannedFile(file, parts));
        }

        return new InstallPlan(plannedFiles, tasks);
    }
}
