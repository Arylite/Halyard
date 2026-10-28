namespace Halyard.Core.Hosting;

/// <summary>Work started once the window is visible, so slow network calls never delay startup.</summary>
public interface IBackgroundStartupTask
{
    Task RunAsync(CancellationToken cancellationToken);
}
