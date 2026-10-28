using Halyard.Models.Installs;
using Halyard.Services.Installs;

namespace Halyard.Services.Tests.Installs;

public sealed class InstallPlannerTests
{
    private static readonly ChunkGuid A = new(0, 0, 0, 1);
    private static readonly ChunkGuid B = new(0, 0, 0, 2);

    private static readonly BuildManifest Manifest = new(
        "App", "1", "a.exe", string.Empty, 18,
        new Dictionary<ChunkGuid, ManifestChunk>
        {
            [A] = new(A, 1, new byte[20], 0, 100, 50),
            [B] = new(B, 2, new byte[20], 0, 100, 50),
        },
        []);

    [Fact]
    public void Build_SharesAChunkReusedWithinTheWindow()
    {
        ManifestFile[] files = [File("one", A, B), File("two", A)];

        var plan = InstallPlanner.Build(files, Manifest, reuseWindow: 10);

        Assert.Equal(2, plan.Tasks.Count);
        Assert.Same(plan.Files[0].Parts[0].Task, plan.Files[1].Parts[0].Task);
        Assert.Equal(2, plan.Files[0].Parts[0].Task.RemainingUses);
        Assert.Equal(100, plan.DownloadBytes);
    }

    [Fact]
    public void Build_DownloadsAgainWhenReuseIsTooFarAway()
    {
        ManifestFile[] files = [File("one", A, B, B, B), File("two", A)];

        var plan = InstallPlanner.Build(files, Manifest, reuseWindow: 2);

        Assert.Equal(3, plan.Tasks.Count);
        Assert.NotSame(plan.Files[0].Parts[0].Task, plan.Files[1].Parts[0].Task);
        Assert.All(plan.Tasks, task => Assert.True(task.RemainingUses > 0));
    }

    private static ManifestFile File(string path, params ChunkGuid[] chunks) =>
        new(path, new byte[20], [], [.. chunks.Select(chunk => new ChunkPart(chunk, 0, 10))]);
}
