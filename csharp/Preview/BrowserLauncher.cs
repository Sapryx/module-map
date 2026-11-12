using System.ComponentModel;
using System.Diagnostics;

namespace ModuleMap.Preview;

public sealed class BrowserLauncher
{
    private readonly TextWriter log;

    public BrowserLauncher(TextWriter log)
    {
        this.log = log;
    }

    public void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch(Win32Exception exception)
        {
            log.WriteLine($"Could not open a browser ({exception.Message}); open {url} yourself");
        }
    }
}
