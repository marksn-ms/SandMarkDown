using System.Windows;

namespace SandMarkDown;

/// <summary>
/// WPF application entry point for SandMarkDown.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Applies the Sandmark dark theme and opens a file path passed on the command line.
    /// </summary>
    /// <param name="e">Startup arguments. A Markdown path opens in the reader.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        // Dark chrome for menus and dialogs. WPF0001 is suppressed in the project file.
        ThemeMode = ThemeMode.Dark;
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        if (FileArgument(e.Args) is string path)
        {
            window.OpenPath(path);
        }
    }

    private static string? FileArgument(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (arg is "-h" or "--help" or "/?")
            {
                continue;
            }

            return arg;
        }

        return null;
    }
}
