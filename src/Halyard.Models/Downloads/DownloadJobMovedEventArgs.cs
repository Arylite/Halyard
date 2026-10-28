namespace Halyard.Models.Downloads;

public sealed class DownloadJobMovedEventArgs(DownloadJob job, int oldIndex, int newIndex) : EventArgs
{
    public DownloadJob Job { get; } = job;

    public int OldIndex { get; } = oldIndex;

    public int NewIndex { get; } = newIndex;
}
