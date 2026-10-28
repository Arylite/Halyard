using Microsoft.UI.Xaml.Controls;

namespace Halyard.UI;

/// <param name="ActionText">Label of an optional button in the notification.</param>
/// <param name="Action">What that button does.</param>
public sealed record Notification(
    string Title,
    string Message,
    InfoBarSeverity Severity,
    string? ActionText = null,
    Action? Action = null);

/// <summary>Shows short, non-blocking messages to the user.</summary>
public interface INotificationService
{
    event EventHandler<Notification>? Raised;

    void Show(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Informational);

    void Show(Notification notification);
}
