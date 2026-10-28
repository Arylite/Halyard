using Halyard.Core.Installs;
using Halyard.Core.Settings;
using Halyard.Core.Storage;
using Halyard.Models.Downloads;
using Halyard.Models.Settings;
using Halyard.Services.Downloads;
using Microsoft.Extensions.Logging.Abstractions;

namespace Halyard.Services.Tests.Downloads;

public sealed class DownloadQueueThreadingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"halyard-queue-{Guid.NewGuid():N}");

    [Fact]
    public async Task Installs_NeverRunOnTheCallersSynchronizationContext()
    {
        var installer = new RecordingInstaller();
        using var queue = new DownloadQueue(installer, new FixedSettings(), new AppPaths(_root), NullLogger<DownloadQueue>.Instance);

        // Simulate the UI: a context the install must not capture.
        var uiContext = new SingleThreadContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(uiContext);
        try
        {
            queue.Enqueue(new InstallRequest("game", "Game", _root, [], InstallKind.Install));
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        var context = await installer.Ran.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.NotSame(uiContext, context);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class RecordingInstaller : IGameInstaller
    {
        public TaskCompletionSource<SynchronizationContext?> Ran { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RunAsync(InstallRequest request, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
        {
            Ran.TrySetResult(SynchronizationContext.Current);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedSettings : ISettingsService
    {
        public LauncherSettings Current { get; } = new();

        public event EventHandler<LauncherSettings>? Changed
        {
            add { }
            remove { }
        }

        public Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>Queues posted work forever, like a UI thread that is busy: nothing posted here may matter.</summary>
    private sealed class SingleThreadContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
        }
    }
}
