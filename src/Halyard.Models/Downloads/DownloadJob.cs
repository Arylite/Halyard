namespace Halyard.Models.Downloads;

/// <summary>Observable state of one queued install. Only the queue implementation mutates it.</summary>
public sealed class DownloadJob : IProgress<DownloadProgress>
{
    internal DownloadJob(InstallRequest request) => Request = request;

    public Guid Id { get; } = Guid.NewGuid();

    public InstallRequest Request { get; }

    public DownloadState State { get; private set; } = DownloadState.Queued;

    public DownloadProgress Progress { get; private set; }

    public string? Error { get; private set; }

    public bool IsFinished => State is DownloadState.Completed or DownloadState.Failed or DownloadState.Canceled;

    /// <summary>Raised on a background thread whenever state or progress changes.</summary>
    public event EventHandler? Changed;

    internal void TransitionTo(DownloadState state, string? error = null)
    {
        State = state;
        Error = error;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    void IProgress<DownloadProgress>.Report(DownloadProgress value)
    {
        Progress = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
