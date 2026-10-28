namespace Halyard.Core.Security;

/// <summary>Encrypts data at rest so that only the current user on this machine can read it back.</summary>
public interface ISecretProtector
{
    byte[] Protect(byte[] plaintext);

    /// <exception cref="System.Security.Cryptography.CryptographicException">The data was protected for another user or machine, or is corrupt.</exception>
    byte[] Unprotect(byte[] protectedData);
}
