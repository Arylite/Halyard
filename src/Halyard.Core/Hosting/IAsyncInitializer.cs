namespace Halyard.Core.Hosting;

/// <summary>A service that must load its state before the UI starts using it.</summary>
public interface IAsyncInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
