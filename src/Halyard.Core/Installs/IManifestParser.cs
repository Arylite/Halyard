using Halyard.Models.Installs;

namespace Halyard.Core.Installs;

public interface IManifestParser
{
    /// <exception cref="InvalidDataException">The data is not a valid manifest.</exception>
    /// <exception cref="NotSupportedException">The manifest uses a format Halyard can't read.</exception>
    BuildManifest Parse(byte[] data);
}
