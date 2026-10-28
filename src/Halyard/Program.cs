using Halyard.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;

namespace Halyard;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Must run first: install, update and uninstall hooks exit the process before the UI exists.
        VelopackApp.Build().Run();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        if (SingleInstance.TryRedirectToRunningInstance())
        {
            return;
        }

        Application.Start(callback =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App(StartupOptions.Parse(args));
        });
    }
}
