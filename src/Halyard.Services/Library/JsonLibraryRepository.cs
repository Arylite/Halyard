using Halyard.Core.Library;
using Halyard.Core.Serialization;
using Halyard.Core.Storage;
using Halyard.Models.Library;

namespace Halyard.Services.Library;

public sealed class JsonLibraryRepository(AppPaths paths) : ILibraryRepository
{
    private readonly JsonFileStore<List<Game>> _store = new(paths.LibraryFile, HalyardJsonContext.Default.ListGame);

    public async Task<IReadOnlyList<Game>> LoadAsync(CancellationToken cancellationToken) =>
        await _store.ReadAsync(cancellationToken) ?? [];

    public Task SaveAsync(IReadOnlyList<Game> games, CancellationToken cancellationToken) =>
        _store.WriteAsync([.. games], cancellationToken);
}
