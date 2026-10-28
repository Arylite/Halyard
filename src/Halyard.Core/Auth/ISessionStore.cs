using Halyard.Models.Auth;

namespace Halyard.Core.Auth;

/// <summary>Persists the signed-in session between runs.</summary>
public interface ISessionStore
{
    Task<EpicSession?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(EpicSession session, CancellationToken cancellationToken);

    void Clear();
}
