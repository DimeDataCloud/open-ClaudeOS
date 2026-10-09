using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace ClaudeOS.Shell;

/// <summary>
/// A custom entry point so a second launch (the Start menu, the Copilot key, a file association)
/// hands over to the running copy instead of starting another. The running copy answers by
/// showing the bar.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var main = AppInstance.FindOrRegisterForKey("open-claudeos-main");
        if (!main.IsCurrent)
        {
            main.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs()).AsTask().GetAwaiter().GetResult();
            return 0;
        }

        main.Activated += (_, _) => App.Current?.OnSecondLaunch();

        Application.Start(callback =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            new App();
        });
        return 0;
    }
}
