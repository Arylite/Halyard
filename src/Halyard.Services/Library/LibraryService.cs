using System.Text.Json;
using Halyard.Core.Auth;
using Halyard.Core.Hosting;
using Halyard.Core.Library;
using Halyard.Models.Library;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Library;

/// <summary>
/// The library is served from its local cache immediately; sources are queried in the background
/// and merged in when they answer.
/// </summary>
public sealed class LibraryService(
    ILibraryRepository repository,
    IEnumerable<IGameSource> sources,
    ILogger<LibraryService> logger) : ILibraryService, IAsyncInitializer, IBackgroundStartupTask, IDisposable
{
    private readonly SemaphoreSlim _mutation = new(1, 1);

    public IReadOnlyList<Game> Games { get; private set; } = [];

    public event EventHandler? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken) =>
        Games = Sort(await repository.LoadAsync(cancellationToken));

    public Task RunAsync(CancellationToken cancellationToken) => RefreshAsync(cancellationToken);

    public Game? Find(string gameId) => Games.FirstOrDefault(game => game.Id == gameId);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var sourceList = sources.ToList();
        var results = await Task.WhenAll(sourceList.Select(source => DiscoverAsync(source, cancellationToken)));
        var discovered = results.SelectMany(games => games ?? []).ToList();

        // Only a launcher scan that worked can tell an install is gone; a failed one says nothing.
        var launcherScanned = Succeeded<EpicLauncherGameSource>(sourceList, results);
        var ownedScanned = Succeeded<EpicOwnedGamesSource>(sourceList, results);

        await MutateAsync(games => LibraryMerger.Merge(games, discovered, launcherScanned, ownedScanned), cancellationToken);
        logger.LogInformation("Library refreshed: {Count} game(s)", Games.Count);
    }

    public async Task AddAsync(Game game, CancellationToken cancellationToken = default)
    {
        await MutateAsync(games => [.. games.Where(existing => existing.Id != game.Id), game], cancellationToken);
        logger.LogInformation("Added {Game} to the library", game.Title);
    }

    public Task UpdateAsync(string gameId, Func<Game, Game?> update, CancellationToken cancellationToken = default) =>
        MutateAsync(
            games => [.. games.Select(game => game.Id == gameId ? update(game) : game).OfType<Game>()],
            cancellationToken);

    public async Task RemoveAsync(string gameId, CancellationToken cancellationToken = default)
    {
        await MutateAsync(games => [.. games.Where(game => game.Id != gameId)], cancellationToken);
        logger.LogInformation("Removed {GameId} from the library", gameId);
    }

    private static bool Succeeded<TSource>(IReadOnlyList<IGameSource> sources, IReadOnlyList<Game>?[] results) =>
        sources.Where((source, i) => source is TSource && results[i] is not null).Any();

    /// <returns><see langword="null"/> when the source failed.</returns>
    private async Task<IReadOnlyList<Game>?> DiscoverAsync(IGameSource source, CancellationToken cancellationToken)
    {
        try
        {
            return await source.DiscoverAsync(cancellationToken);
        }
        catch (Exception ex) when (
            ex is EpicAuthException or HttpRequestException or IOException or UnauthorizedAccessException or JsonException ||
            (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "{Source} is unavailable; keeping its cached games", source.Name);
            return null;
        }
    }

    private async Task MutateAsync(Func<IReadOnlyList<Game>, IReadOnlyList<Game>> mutation, CancellationToken cancellationToken)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var updated = Sort(mutation(Games));
            await repository.SaveAsync(updated, cancellationToken);
            Games = updated;
        }
        finally
        {
            _mutation.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyList<Game> Sort(IEnumerable<Game> games) =>
        [.. games.OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase)];

    public void Dispose() => _mutation.Dispose();
}
