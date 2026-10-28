using System.Security.Cryptography;
using Halyard.Core.Security;

namespace Halyard.Services.Security;

/// <summary>Windows DPAPI, scoped to the current user.</summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    // Ties blobs to Halyard so other DPAPI consumers running as the same user can't be fed them by mistake.
    private static readonly byte[] Entropy = "Halyard.v1"u8.ToArray();

    public byte[] Protect(byte[] plaintext) =>
        ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] protectedData) =>
        ProtectedData.Unprotect(protectedData, Entropy, DataProtectionScope.CurrentUser);
}
