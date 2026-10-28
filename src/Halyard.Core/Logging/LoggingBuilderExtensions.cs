using Halyard.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Halyard.Core.Logging;

public static class LoggingBuilderExtensions
{
    /// <summary>Routes logs to the in-memory store (for the UI) and to daily files.</summary>
    public static ILoggingBuilder AddHalyardSinks(this ILoggingBuilder builder)
    {
        var services = builder.Services;

        services.AddSingleton<InMemoryLogStore>();
        services.AddSingleton<ILogSink>(sp => sp.GetRequiredService<InMemoryLogStore>());

        services.AddSingleton(sp => new FileLogSink(sp.GetRequiredService<AppPaths>().LogsDirectory));
        services.AddSingleton<ILogSink>(sp => sp.GetRequiredService<FileLogSink>());

        services.AddSingleton<ILoggerProvider, SinkLoggerProvider>();
        return builder;
    }
}
