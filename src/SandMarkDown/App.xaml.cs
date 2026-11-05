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

    // Window brush colors from docs/tokens.css, dark then light.
    private static readonly (string Key, string Dark, string Light)[] Palette =
    [
        ("SmtPage", "#101418", "#F6F5F2"),
        ("SmtInk", "#E7EEF3", "#1C2430"),
        ("SmtMuted", "#9AA6B2", "#5C6774"),
        ("SmtSurface", "#1A2129", "#FFFFFF"),
        ("SmtLine", "#2C3642", "#D9DDE3"),
        ("SmtAccent", "#8EC0E0", "#1F5678"),
        ("SmtAccentInk", "#8EC0E0", "#163E58"),
        ("SmtOnAccent", "#101418", "#FFFFFF"),
        ("SmtDanger", "#F0A8A8", "#9B2C2C"),
        ("AccentBrushHover", "#A9D0E8", "#163E58"),
    ];

    /// <summary>
    /// Settings file for the View menu.
    /// </summary>
    internal static string ViewSettingsPath { get; } = Path.Combine(DataFolder, "view.json");

    /// <summary>
    /// Switches window chrome and brushes between the dark and light Sandmark palettes.
    /// </summary>
    /// <param name="dark">True for the dark palette.</param>
    internal static void ApplyTheme(bool dark)
    {
        if (Current is not App app)
        {
            return;
        }

        // WPF0001: ThemeMode is marked experimental. It is suppressed in the project file.
        app.ThemeMode = dark ? ThemeMode.Dark : ThemeMode.Light;
        foreach (var (key, darkColor, lightColor) in Palette)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? darkColor : lightColor);
            var brush = new System.Windows.Media.SolidColorBrush(color);
            brush.Freeze();
            app.Resources[key] = brush;
        }
    }

    /// <summary>
    /// The page background for the current palette, for WebView2's first paint.
    /// </summary>
    /// <param name="dark">True for the dark palette.</param>
    /// <returns>The page color.</returns>
    internal static System.Drawing.Color PageColor(bool dark) =>
        dark ? System.Drawing.Color.FromArgb(0x10, 0x14, 0x18) : System.Drawing.Color.FromArgb(0xF6, 0xF5, 0xF2);

    /// <summary>
    /// Applies the Sandmark dark theme and opens a file path passed on the command line.
    /// </summary>
    /// <param name="e">Startup arguments. A Markdown path opens in the reader.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);
        ApplyTheme(ViewSettings.Load(ViewSettingsPath).DarkMode);

        var jumpList = new System.Windows.Shell.JumpList { ShowRecentCategory = true };
        System.Windows.Shell.JumpList.SetJumpList(this, jumpList);
        jumpList.Apply();

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
