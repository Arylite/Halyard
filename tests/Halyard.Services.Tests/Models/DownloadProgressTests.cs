using Halyard.Models.Downloads;

namespace Halyard.Services.Tests.Models;

public sealed class DownloadProgressTests
{
    [Fact]
    public void Default_HasNoNullMembers()
    {
        // A queued job starts with default(DownloadProgress); the UI reads it right away.
        var progress = default(DownloadProgress);

        Assert.Empty(progress.Cdns);
        Assert.Null(progress.Fraction);
        Assert.Null(progress.Remaining);
    }
}
