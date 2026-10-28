using System.ComponentModel;
using Halyard.Core.Auth;
using Halyard.Core.Downloads;
using Halyard.Core.Launching;
using Halyard.Core.Library;
using Halyard.Models.Downloads;
using Halyard.Models.Library;
using Halyard.UI;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.ViewModels;

/// <param name="Action">What Enter does, as the library card's main button would.</param>
public sealed record QuickSearchResult(Game Game, string Action)
{
    public string Title => Game.Title;

    // AutoSuggestBox shows ToString() in its text box once a suggestion is chosen.
    public override string ToString() => Title;
}

/// <summary>The title bar search: find any game, Enter plays it or starts its install.</summary>
public sealed class QuickSearchViewModel(
    ILibraryService library,
    IGameLauncher launcher,
    IDownloadQueue queue,
    IDialogService dialogs,
    INavigationService navigation,
    INotificationService notifications)
{
    private const int MaxResults = 8;

    public IReadOnlyList<QuickSearchResult> Search(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var query = text.Trim();
        return [.. library.Games
            .Where(game => game.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(game => game.Title.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
            .ThenByDescending(game => game.IsInstalled)
            .ThenByDescending(game => game.LastPlayedAt ?? DateTimeOffset.MinValue)
            .Take(MaxResults)
            .Select(game => new QuickSearchResult(game, ActionText(game)))];
    }

    private string ActionText(Game game) => game switch
    {
        _ when launcher.IsRunning(game.Id) => "Playing",
        _ when queue.FindActive(game.Id) is not null => "View download",
        { HasUpdate: true } => "Update",
        { IsInstalled: true } => "Play",
        { IsDownloadable: true } => "Install",
        _ => "Show in library",
    };

    public async Task ActivateAsync(QuickSearchResult result)
    {
        // The library may have changed since the suggestion was built.
        var game = library.Find(result.Game.Id) ?? result.Game;

        // Same as the card: a running game is left alone (no update under its feet), a busy one
        // shows its download, an outdated one updates first.
        if (launcher.IsRunning(game.Id))
        {
            return;
        }

        if (queue.FindActive(game.Id) is not null)
        {
            navigation.NavigateTo(PageKeys.Downloads);
        }
        else if (game is { HasUpdate: true, Installation: { } installation })
        {
            queue.Enqueue(new InstallRequest(game.Id, game.Title, installation.InstallDirectory, installation.InstallTags, InstallKind.Update));
            navigation.NavigateTo(PageKeys.Downloads);
        }
        else if (game.IsInstalled)
        {
            try
            {
                await launcher.LaunchAsync(game);
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or EpicAuthException or HttpRequestException or Win32Exception)
            {
                notifications.Show($"Couldn't launch {game.Title}", ex.Message, InfoBarSeverity.Error);
            }
        }
        else if (game.IsDownloadable)
        {
            if (await dialogs.ShowInstallAsync(game) is { } request)
            {
                queue.Enqueue(request);
                navigation.NavigateTo(PageKeys.Downloads);
            }
        }
        else
        {
            navigation.NavigateTo(PageKeys.Library);
        }
    }
}
