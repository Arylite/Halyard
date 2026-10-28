using Halyard.Models.Library;

namespace Halyard.Core.Library;

/// <summary>Finds games installed by something other than Halyard (another launcher, a known folder...).</summary>
public interface IGameSource
{
    string Name { get; }

    Task<IReadOnlyList<Game>> DiscoverAsync(CancellationToken cancellationToken);
}
