namespace Halyard.UI;

/// <summary>Hands paths and links over to Windows (Explorer, default browser).</summary>
public interface IShellService
{
    void OpenFolder(string path);

    void OpenUri(Uri uri);
}
