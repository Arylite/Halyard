using Halyard.Core.Auth;
using Halyard.Core.Hosting;
using Halyard.Core.Library;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Library;

/// <summary>Refreshes the library when the account changes: its games show up, the previous one's leave.</summary>
public sealed class LibraryAccountSync(IAuthService auth, ILibraryService library, ILogger<LibraryAccountSync> logger)
    : IBackgroundStartupTask, IDisposable
{
    private string? _accountId = auth.Session?.AccountId;

    public Task RunAsync(CancellationToken cancellationToken)
    {
        auth.SessionChanged += OnSessionChanged;
        return Task.CompletedTask;
    }

    private async void OnSessionChanged(object? sender, EventArgs e)
    {
        // Signing out refreshes too: the previous account's games leave the library.
        var accountId = auth.Session?.AccountId;
        if (accountId == Interlocked.Exchange(ref _accountId, accountId))
        {
            return;
        }

        try
        {
            await library.RefreshAsync();
        }
        catch (Exception ex)
        {
            // async void: anything escaping here would end Halyard.
            logger.LogWarning(ex, "Library refresh after sign-in failed");
        }
    }

    public void Dispose() => auth.SessionChanged -= OnSessionChanged;
}
