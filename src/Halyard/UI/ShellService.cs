using System.Diagnostics;

namespace Halyard.UI;

public sealed class ShellService : IShellService
{
    public void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Open(path);
    }

    public void OpenUri(Uri uri) => Open(uri.AbsoluteUri);

    private static void Open(string target) =>
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
}
