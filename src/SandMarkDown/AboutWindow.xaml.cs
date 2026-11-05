using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using SandMarkDown.Core;

namespace SandMarkDown;

/// <summary>
/// About dialog. Its text types itself out as Markdown, then renders with the reader's own pipeline.
/// </summary>
public partial class AboutWindow : Window
{
    private const string AboutUrl = "https://about.sandmarkdown.local/";
    private const string CanyonHelp = "Left and Right steer. Esc returns.";

    private readonly DiagnosticReport _report;
    private readonly DispatcherTimer _canyonTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly DispatcherTimer _canyonEndTimer = new() { Interval = TimeSpan.FromMilliseconds(1400) };
    private CanyonRound? _round;
    private string _html = string.Empty;
    private string _policy = string.Empty;

    /// <summary>
    /// Initializes the About dialog.
    /// </summary>
    public AboutWindow()
    {
        InitializeComponent();
        Title = $"About {Branding.ProductName}{Branding.ProductTitleSuffix}";
        _report = CreateReport(webView2Version: null);
        _canyonTimer.Tick += (_, _) => AdvanceCanyon();
        _canyonEndTimer.Tick += (_, _) => EndCanyon();
    }

    /// <summary>
    /// Stops a Canyon round when the dialog closes.
    /// </summary>
    /// <param name="e">Close event data.</param>
    protected override void OnClosed(EventArgs e)
    {
        _canyonTimer.Stop();
        _canyonEndTimer.Stop();
        base.OnClosed(e);
    }

    /// <summary>
    /// Builds version details for the About box and for support requests.
    /// </summary>
    /// <param name="webView2Version">WebView2 runtime version, when known.</param>
    /// <returns>Version, commit, Windows, .NET, and WebView2 details.</returns>
    internal static DiagnosticReport CreateReport(string? webView2Version)
    {
        var assembly = typeof(AboutWindow).Assembly;
        var fallback = assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var (version, commit) = DiagnosticReport.ParseInformationalVersion(informational, fallback);
        return new DiagnosticReport(
            version,
            commit,
            $"{RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}",
            RuntimeInformation.FrameworkDescription,
            webView2Version);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, App.WebViewDataFolder);
            await Page.EnsureCoreWebView2Async(environment);
            var report = _report with { WebView2 = environment.BrowserVersionString };
            var about = AboutPage.Build(report, LogoDataUri());
            _html = about.Html;
            _policy = about.ContentSecurityPolicy;

            var core = Page.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.AddWebResourceRequestedFilter(AboutUrl + "*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += OnWebResourceRequested;
            core.NavigationStarting += OnNavigationStarting;
            core.NewWindowRequested += OnNewWindowRequested;
            core.WebMessageReceived += (_, args) => OnMessage(args.WebMessageAsJson, report);
            core.Navigate(AboutUrl);
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or COMException or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, Branding.ProductName, MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
        }
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        e.Response = Page.CoreWebView2.Environment.CreateWebResourceResponse(
            new MemoryStream(Encoding.UTF8.GetBytes(_html)),
            200,
            "OK",
            "Content-Type: text/html; charset=utf-8\r\nContent-Security-Policy: " + _policy);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.Equals(AboutUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;
        if (e.IsUserInitiated)
        {
            OpenWebLink(e.Uri);
        }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (e.IsUserInitiated)
        {
            OpenWebLink(e.Uri);
        }
    }

    private void OnMessage(string json, DiagnosticReport report)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("type", out var type))
            {
                return;
            }

            switch (type.GetString())
            {
                case "copy":
                    Clipboard.SetText(report.ToString());
                    break;
                case "close":
                    Close();
                    break;
                case "canyon":
                    StartCanyon();
                    break;
            }
        }
        catch (Exception ex) when (ex is JsonException or COMException or InvalidOperationException)
        {
            // A failed clipboard write or an odd message is not worth interrupting the dialog for.
        }
    }

    private static void OpenWebLink(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // No browser is registered. The link text still shows where to go.
        }
    }

    private static string LogoDataUri()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/logo-small-narrow.jpg"));
        using var stream = resource.Stream;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return "data:image/jpeg;base64," + Convert.ToBase64String(memory.ToArray());
    }

    // The About page owns the keyboard and reports S, M, T. During a round the window steers.
    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_round is null && !_canyonEndTimer.IsEnabled)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }

            return;
        }

        switch (e.Key)
        {
            case Key.Left:
                SteerCanyon(-1);
                e.Handled = true;
                break;
            case Key.Right:
                SteerCanyon(1);
                e.Handled = true;
                break;
            case Key.Escape:
                EndCanyon();
                e.Handled = true;
                break;
        }
    }

    private void StartCanyon()
    {
        _canyonEndTimer.Stop();
        _round = new CanyonRound();
        CanyonView.Text = _round.Picture();
        CanyonStatus.Text = CanyonHelp;
        Page.Visibility = Visibility.Collapsed;
        CanyonCard.Visibility = Visibility.Visible;
        CanyonHost.Focus();
        _canyonTimer.Start();
    }

    private void AdvanceCanyon()
    {
        if (_round is null)
        {
            return;
        }

        var crashed = _round.Advance();
        CanyonView.Text = _round.Picture();
        if (crashed)
        {
            Crash();
        }
    }

    private void SteerCanyon(int direction)
    {
        if (_round is null)
        {
            return;
        }

        var crashed = _round.Steer(direction);
        CanyonView.Text = _round.Picture();
        if (crashed)
        {
            Crash();
        }
    }

    private void Crash()
    {
        _canyonTimer.Stop();
        CanyonStatus.Text = $"Hit the wall after {_round?.Distance ?? 0} rows.";
        _round = null;
        _canyonEndTimer.Start();
    }

    private void EndCanyon()
    {
        _canyonTimer.Stop();
        _canyonEndTimer.Stop();
        _round = null;
        CanyonCard.Visibility = Visibility.Collapsed;
        CanyonView.Text = string.Empty;
        Page.Visibility = Visibility.Visible;
        Page.Focus();
    }
}
