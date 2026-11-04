using System.IO;
using System.Windows;
using System.Windows.Threading;
using SandMarkDown.Core;

namespace SandMarkDown;

/// <summary>
/// WPF application entry point for SandMarkDown.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Per-user folder for settings and the error log.
    /// </summary>
    internal static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Sandmark Technologies",
        "SandMarkDown");

    /// <summary>
    /// WebView2 profile folder shared by the reader and the About box.
    /// </summary>
    internal static string WebViewDataFolder { get; } = Path.Combine(DataFolder, "WebView2");

    private bool _reportingError;

    /// <summary>
    /// Applies the Sandmark dark theme and opens a file path passed on the command line.
    /// </summary>
    /// <param name="e">Startup arguments. A Markdown path opens in the reader.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

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

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // The window stays open. The reader can keep going or close it.
        e.Handled = true;
        ReportError(e.Exception, canContinue: true);
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogError(exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogError(e.Exception);
        e.SetObserved();
    }

    private void ReportError(Exception exception, bool canContinue)
    {
        var logPath = LogError(exception);
        if (_reportingError)
        {
            return;
        }

        _reportingError = true;
        try
        {
            var where = logPath is null ? string.Empty : $"\n\nDetails were saved to:\n{logPath}";
            var next = canContinue ? "You can keep reading." : "SandMarkDown needs to close.";
            MessageBox.Show(
                MainWindow,
                $"Something went wrong: {exception.Message}\n\n{next}{where}\n\nTo report it, use Help, About, Report a problem.",
                Branding.ProductName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _reportingError = false;
        }
    }

    private static string? LogError(Exception exception)
    {
        try
        {
            var version = AboutWindow.CreateReport(webView2Version: null).DisplayVersion;
            return ErrorLog.Write(Path.Combine(DataFolder, "logs"), exception, DateTimeOffset.Now, version);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
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
