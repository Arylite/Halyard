namespace Halyard.UI;

/// <summary>Marshals work onto the UI thread; core services raise their events on background threads.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}
