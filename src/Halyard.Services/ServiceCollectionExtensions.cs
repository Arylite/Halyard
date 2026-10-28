using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using Halyard.Core.Auth;
using Halyard.Core.Diagnostics;
using Halyard.Services.Diagnostics;
using Halyard.Core.Downloads;
using Halyard.Core.Epic;
using Halyard.Core.Hosting;
using Halyard.Core.Installs;
using Halyard.Core.Launching;
using Halyard.Core.Library;
using Halyard.Core.Logging;
using Halyard.Core.Security;
using Halyard.Core.Settings;
using Halyard.Core.Storage;
using Halyard.Services.Auth;
using Halyard.Services.Downloads;
using Halyard.Services.Epic;
using Halyard.Services.Installs;
using Halyard.Services.Launching;
using Halyard.Services.Library;
using Halyard.Services.Security;
using Halyard.Services.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Halyard.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers every launcher service. Initializers run in registration order.</summary>
    public static IServiceCollection AddHalyardServices(this IServiceCollection services, AppPaths paths)
    {
        services.AddSingleton(paths);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(CreateHttpClient());
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Debug).AddHalyardSinks());

        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddInitializedSingleton<ISettingsService, SettingsService>();

        // Auth
        services.AddSingleton<IEpicWebClient, EpicWebClient>();
        services.AddSingleton<IEpicOAuthClient, EpicOAuthClient>();
        services.AddSingleton<ISessionStore, ProtectedSessionStore>();
        services.AddInitializedSingleton<IAuthService, AuthService>();
        services.AddSingleton<IAccessTokenProvider>(sp => sp.GetRequiredService<AuthService>());
        services.AddSingleton<IEpicLauncherApi, EpicLauncherApi>();

        // Library
        services.AddSingleton<ILibraryRepository, JsonLibraryRepository>();
        services.AddSingleton<IGameSource, EpicLauncherGameSource>();
        services.AddSingleton<IGameSource, EpicOwnedGamesSource>();
        services.AddInitializedSingleton<ILibraryService, LibraryService>();
        services.AddSingleton<IBackgroundStartupTask>(sp => sp.GetRequiredService<LibraryService>());
        services.AddSingleton<IBackgroundStartupTask, LibraryAccountSync>();
        services.AddSingleton<IBackgroundStartupTask, PlaytimeTracker>();

        // Installs
        services.AddSingleton<IManifestParser, BinaryManifestParser>();
        services.AddSingleton<IManifestSource, EpicManifestSource>();
        services.AddSingleton<IInstalledManifestStore, InstalledManifestStore>();
        services.AddSingleton<IInstallPreparer, InstallPreparer>();
        services.AddSingleton<IBandwidthLimiter, TokenBucketBandwidthLimiter>();
        services.AddSingleton<ChunkDownloader>();
        services.AddSingleton<InstallPipeline>();
        services.AddSingleton<IGameInstaller, GameInstaller>();
        services.AddSingleton<IGameUninstaller, GameUninstaller>();
        services.AddSingleton<IPrerequisiteInstaller, PrerequisiteInstaller>();
        services.AddInitializedSingleton<IDownloadQueue, DownloadQueue>();
        services.AddSingleton<IBackgroundStartupTask, PauseWhilePlaying>();
        services.AddSingleton<IBackgroundStartupTask, AutoGameUpdater>();

        services.AddSingleton<IDiagnosticsExporter, DiagnosticsExporter>();

        // Launching
        services.AddSingleton<ILaunchArgumentsBuilder, EpicLaunchArgumentsBuilder>();
        services.AddSingleton<IGameLauncher, GameLauncher>();

        return services;
    }

    // The annotation keeps the implementation's constructor through trimming (Native AOT builds).
    private static void AddInitializedSingleton<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService, IAsyncInitializer
    {
        services.AddSingleton<TImplementation>();
        services.AddSingleton<TService>(sp => sp.GetRequiredService<TImplementation>());
        services.AddSingleton<IAsyncInitializer>(sp => sp.GetRequiredService<TImplementation>());
    }

    private static HttpClient CreateHttpClient()
    {
        // One long-lived client with recycled connections, sized for many parallel chunk downloads.
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            MaxConnectionsPerServer = 64,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            EnableMultipleHttp2Connections = true,
        };

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Halyard/{version}");
        return client;
    }
}
