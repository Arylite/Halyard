using Halyard.Core.Hosting;
using Halyard.Core.Settings;
using Halyard.Core.Storage;
using Halyard.Hosting;
using Halyard.Platform;
using Halyard.Services;
using Halyard.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Halyard;

public partial class App : Application
{
    private readonly StartupOptions _options;
    private ServiceProvider? _services;
    private ILogger<App>? _logger;

    public App(StartupOptions options)
    {
        _options = options;
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnFatalException;
    }

    /// <summary>Resolves a service for XAML-instantiated types (pages) that can't use constructor injection.</summary>
    public static T GetRequiredService<T>()
        where T : notnull =>
        (((App)Current)._services ?? throw new InvalidOperationException("Services are not built yet."))
            .GetRequiredService<T>();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _services = new ServiceCollection()
            .AddHalyardServices(AppPaths.Default)
            .AddHalyardUi(DispatcherQueue.GetForCurrentThread(), _options)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        _logger = _services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("Halyard {Version} starting", typeof(App).Assembly.GetName().Version?.ToString(3));

        try
        {
            foreach (var initializer in _services.GetServices<IAsyncInitializer>())
            {
                await initializer.InitializeAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            // Start anyway: every page works with empty state, and the error is in the logs.
            _logger.LogError(ex, "Startup initialization failed");
        }

        var window = _services.GetRequiredService<MainWindow>();
        var presenter = _services.GetRequiredService<WindowPresenter>();
        var settings = _services.GetRequiredService<ISettingsService>();

        // Formatting is static; it follows the speed unit setting from here on.
        Formatting.ByteSize.SpeedInBits = settings.Current.ShowSpeedInBits;
        settings.Changed += (_, current) => Formatting.ByteSize.SpeedInBits = current.ShowSpeedInBits;

        window.AppWindow.Closing += (_, closing) => OnWindowClosing(closing, presenter, settings);
        window.Closed += OnMainWindowClosed;
        SingleInstance.Reactivated += (_, _) => presenter.Show();

        // Started with Windows: stay in the tray until the user asks for the window.
        if (!_options.StartHidden)
        {
            window.Activate();
        }

        await RunBackgroundStartupTasksAsync(_services, _logger);
    }

    /// <summary>Closing hides to the tray (downloads keep going), unless the user chose Exit.</summary>
    private static void OnWindowClosing(AppWindowClosingEventArgs closing, WindowPresenter presenter, ISettingsService settings)
    {
        if (settings.Current.CloseToTray && !presenter.IsExiting)
        {
            closing.Cancel = true;
            presenter.Hide();
        }
    }

    /// <summary>
    /// Startup work that can wait for the window (library refresh, tray icon...). Tasks start together
    /// on the UI thread, so a slow network call never holds back the others.
    /// </summary>
    private static Task RunBackgroundStartupTasksAsync(IServiceProvider services, ILogger logger) =>
        Task.WhenAll(services.GetServices<IBackgroundStartupTask>().Select(async task =>
        {
            try
            {
                await task.RunAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background startup task {Task} failed", task.GetType().Name);
            }
        }));

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        _logger?.LogInformation("Halyard shutting down");

        // Disposing flushes the file log, removes the tray icon and cancels running downloads
        // (their files are kept and resume next time).
        _services?.Dispose();
        Exit();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        _logger?.LogCritical(e.Exception, "Unhandled UI exception");
        e.Handled = true;

        _services?.GetService<INotificationService>()
            ?.Show("Something went wrong", e.Exception.Message, InfoBarSeverity.Error);
    }

    /// <summary>Last chance before the process dies: get the crash into the log file for bug reports.</summary>
    private void OnFatalException(object sender, System.UnhandledExceptionEventArgs e)
    {
        _logger?.LogCritical(e.ExceptionObject as Exception, "Fatal unhandled exception");

        // Disposing flushes the file log; the process is going down anyway.
        _services?.Dispose();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}
