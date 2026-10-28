using Halyard.Core.Hosting;
using Halyard.Platform;
using Halyard.UI;
using Halyard.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

namespace Halyard.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHalyardUi(
        this IServiceCollection services,
        DispatcherQueue dispatcherQueue,
        StartupOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton(dispatcherQueue);
        services.AddSingleton<IUiDispatcher>(new DispatcherQueueUiDispatcher(dispatcherQueue));
        services.AddSingleton<WindowContext>();
        services.AddSingleton<IWindowContext>(sp => sp.GetRequiredService<WindowContext>());
        services.AddSingleton<IEpicLoginPrompt, WebViewEpicLoginPrompt>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IPickerService, PickerService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IDialogService, DialogService>();

        // Windows integration
        services.AddSingleton<WindowPresenter>();
        services.AddSingleton<StartupRegistration>();
        services.AddSingleton<TaskbarProgress>();
        services.AddSingleton<ToastNotifier>();
        services.AddSingleton<DownloadActivity>();
        services.AddSingleton<AppUpdater>();
        services.AddSingleton<TrayIcon>();
        services.AddSingleton<IBackgroundStartupTask>(sp => sp.GetRequiredService<TrayIcon>());
        services.AddSingleton<IBackgroundStartupTask, TaskbarProgressReporter>();
        services.AddSingleton<IBackgroundStartupTask, DownloadNotifications>();
        services.AddSingleton<IBackgroundStartupTask>(sp => sp.GetRequiredService<AppUpdater>());
        services.AddSingleton<IBackgroundStartupTask, WelcomeExperience>();
        services.AddSingleton<IBackgroundStartupTask, SessionExpiryNotifier>();

        // View models are singletons so their state survives page navigation.
        services.AddSingleton<GameItemViewModelFactory>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<DownloadsViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AccountViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<QuickSearchViewModel>();

        services.AddSingleton<MainWindow>();
        return services;
    }
}
