using Halyard.Models.Library;

namespace Halyard.Core.Library;

public interface ILibraryRepository
{
    Task<IReadOnlyList<Game>> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(IReadOnlyList<Game> games, CancellationToken cancellationToken);
}
