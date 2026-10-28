using Halyard.Core.Downloads;
using Halyard.Models.Downloads;
using Halyard.Models.Installs;
using Halyard.Services.Installs;
using Halyard.Services.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Halyard.Services.Tests.Installs;

public sealed class InstallPipelineTests : IDisposable
{
    private static readonly Uri[] Cdns =
    [
        new("https://cdn-a.test/build/"),
        new("https://cdn-b.test/Builds/Fortnite/"),
        new("https://cdn-c.test/"),
    ];

    private readonly string _installDirectory = Path.Combine(Path.GetTempPath(), $"halyard-test-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(1, 64, false, false)]
    [InlineData(8, 64, false, false)]
    [InlineData(8, 4, false, false)] // tiny cache: shared chunks get evicted and downloaded again
    [InlineData(4, 16, true, false)] // every chunk fails once before succeeding
    [InlineData(8, 64, false, true)] // multi-CDN: spread and hedged
    [InlineData(8, 16, true, true)] // multi-CDN with failures
    public async Task Run_WritesEveryFileByteForByte(int workers, int maxBufferedChunks, bool flakyCdn, bool multiCdn)
    {
        var build = FakeBuild.Create(chunkCount: 40, fileCount: 60);
        var handler = new FakeCdnHandler(Cdns, build.ChunkFiles, flakyCdn);

        await RunAsync(build, build.Manifest.Files, handler, workers, maxBufferedChunks, multiCdn);

        await AssertInstalledAsync(build);
        if (multiCdn)
        {
            Assert.Equal(Cdns.Length, handler.RequestsPerHost.Count);
        }
    }

    [Fact]
    public async Task Run_MultiCdnRoutesAroundASlowCdn()
    {
        var build = FakeBuild.Create(chunkCount: 30, fileCount: 30);
        var handler = new FakeCdnHandler(Cdns, build.ChunkFiles, slowHost: Cdns[0].Host);

        // Every chunk stuck on the slow CDN must be rescued by a hedged request elsewhere.
        await RunAsync(build, build.Manifest.Files, handler, workers: 8, maxBufferedChunks: 64, multiCdn: true);

        await AssertInstalledAsync(build);
    }

    [Fact]
    public async Task Run_OnlyDownloadsTheChunksSelectedFilesNeed()
    {
        var build = FakeBuild.Create(chunkCount: 40, fileCount: 60);
        var handler = new FakeCdnHandler(Cdns, build.ChunkFiles);
        var required = build.Manifest.Files.Where(file => file.InstallTags.Count == 0).ToList();

        // A cache larger than the build: every chunk is downloaded exactly once.
        await RunAsync(build, required, handler, workers: 4, maxBufferedChunks: 4096, multiCdn: false);

        var neededChunks = required.SelectMany(file => file.Parts).Select(part => part.Chunk).Distinct().Count();
        Assert.Equal(neededChunks, handler.RequestCount);
        Assert.All(build.Manifest.Files.Except(required), file => Assert.False(File.Exists(Path.Combine(_installDirectory, file.Path))));
    }

    private async Task RunAsync(
        FakeBuild build,
        IReadOnlyList<ManifestFile> files,
        FakeCdnHandler handler,
        int workers,
        int maxBufferedChunks,
        bool multiCdn)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var http = new HttpClient(handler);
        var pipeline = new InstallPipeline(new ChunkDownloader(http, new UnlimitedBandwidth(), NullLogger<ChunkDownloader>.Instance));
        var plan = InstallPlanner.Build(files, build.Manifest, reuseWindow: maxBufferedChunks / 2);

        await using var journal = await InstallJournal.OpenAsync(_installDirectory, build.Manifest.BuildVersion, resume: false, cancellationToken);
        await using var tracker = new InstallProgressTracker(new Progress<DownloadProgress>(), TimeProvider.System);

        await pipeline.RunAsync(plan, build.Manifest, _installDirectory, Cdns, workers, maxBufferedChunks, () => multiCdn, journal, tracker, cancellationToken);

        Assert.All(files, file => Assert.True(journal.IsCompleted(file.Path)));
    }

    private async Task AssertInstalledAsync(FakeBuild build)
    {
        foreach (var (path, content) in build.FileContents)
        {
            Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(_installDirectory, path), TestContext.Current.CancellationToken));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_installDirectory))
        {
            Directory.Delete(_installDirectory, recursive: true);
        }
    }

    private sealed class UnlimitedBandwidth : IBandwidthLimiter
    {
        public ValueTask WaitAsync(int bytes, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
