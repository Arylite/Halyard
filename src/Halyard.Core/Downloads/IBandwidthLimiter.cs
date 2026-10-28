namespace Halyard.Core.Downloads;

/// <summary>Caps the combined download speed of every install.</summary>
public interface IBandwidthLimiter
{
    /// <summary>Waits until <paramref name="bytes"/> may be transferred.</summary>
    ValueTask WaitAsync(int bytes, CancellationToken cancellationToken);
}
