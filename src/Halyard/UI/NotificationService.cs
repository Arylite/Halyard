using Microsoft.UI.Xaml.Controls;

namespace Halyard.UI;

public sealed class NotificationService : INotificationService
{
    public event EventHandler<Notification>? Raised;

    public void Show(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Informational) =>
        Show(new Notification(title, message, severity));

    public void Show(Notification notification) => Raised?.Invoke(this, notification);
}
