using System.Security.Cryptography;
using System.Text.Json;
using Halyard.Core.Auth;
using Halyard.Core.Security;
using Halyard.Core.Serialization;
using Halyard.Core.Storage;
using Halyard.Models.Auth;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Auth;

/// <summary>Stores the session encrypted, so tokens never sit on disk in clear text.</summary>
public sealed class ProtectedSessionStore(
    AppPaths paths,
    ISecretProtector protector,
    ILogger<ProtectedSessionStore> logger) : ISessionStore
{
    public async Task<EpicSession?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.SessionFile))
        {
            return null;
        }

        try
        {
            var encrypted = await File.ReadAllBytesAsync(paths.SessionFile, cancellationToken);
            return JsonSerializer.Deserialize(protector.Unprotect(encrypted), HalyardJsonContext.Default.EpicSession);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            logger.LogWarning(ex, "Stored session is unreadable and was discarded");
            Clear();
            return null;
        }
    }

    public async Task SaveAsync(EpicSession session, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(session, HalyardJsonContext.Default.EpicSession);

        Directory.CreateDirectory(Path.GetDirectoryName(paths.SessionFile)!);

        // Write then swap: a crash mid-write must not destroy the session and force a new sign-in.
        var temporaryPath = paths.SessionFile + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, protector.Protect(json), cancellationToken);
        File.Move(temporaryPath, paths.SessionFile, overwrite: true);
    }

    public void Clear() => File.Delete(paths.SessionFile);
}
