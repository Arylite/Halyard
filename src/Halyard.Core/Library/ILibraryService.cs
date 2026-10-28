using Halyard.Models.Library;

namespace Halyard.Core.Library;

public interface ILibraryService
{
    /// <summary>All known games, sorted by title.</summary>
    IReadOnlyList<Game> Games { get; }

    event EventHandler? Changed;

    Game? Find(string gameId);

    /// <summary>Re-reads every <see cref="IGameSource"/>. Installations and manual games are kept.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Game game, CancellationToken cancellationToken = default);

    /// <summary>Atomically replaces a game with <paramref name="update"/>'s result; no-op if it is gone.</summary>
    Task UpdateAsync(string gameId, Func<Game, Game?> update, CancellationToken cancellationToken = default);

    Task RemoveAsync(string gameId, CancellationToken cancellationToken = default);
}
