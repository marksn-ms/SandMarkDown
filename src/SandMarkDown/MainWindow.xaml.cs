using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using SandMarkDown.Core;

namespace SandMarkDown;

/// <summary>
/// Reads one Markdown file at a time. There is no editor.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Dictionary<string, string> _resources = new(StringComparer.Ordinal);
    private RenderedDocument? _pending;
    private string? _documentPath;
    private string? _pendingFragment;
    private string _html = string.Empty;
    private double _zoom = ZoomMath.Actual;
    private bool _webReady;

    /// <summary>
    /// Initializes the reader window.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        Title = Branding.ProductName + Branding.ProductTitleSuffix;
    }

    /// <summary>
    /// Reads <paramref name="path"/> and shows it. Missing files and read errors are reported in a dialog.
    /// </summary>
    /// <param name="path">Markdown file to open.</param>
    public void OpenPath(string path)
    {
        OpenDocument(path, fragment: null);
    }

    private void OpenDocument(string path, string? fragment)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                ShowError($"File not found:\n{fullPath}");
                return;
            }

            var markdown = File.ReadAllText(fullPath);
            var rendered = MarkdownHtmlRenderer.Render(markdown, fullPath);
            _documentPath = fullPath;
            _pendingFragment = string.IsNullOrEmpty(fragment) ? null : fragment;
            CloseMenuItem.IsEnabled = true;
            StatusText.Text = fullPath;
            Title = $"{Path.GetFileName(fullPath)} - {Branding.ProductName}{Branding.ProductTitleSuffix}";
            ShowRendered(rendered);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeViewerAsync();
    }

    private async Task InitializeViewerAsync()
    {
        try
        {
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sandmark Technologies",
                "SandMarkDown",
                "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, userData);
            await Viewer.EnsureCoreWebView2Async(environment);
            ConfigureViewer();
            _webReady = true;
            SetZoom(_zoom);

            if (_pending is not null)
            {
                var pending = _pending;
                _pending = null;
                ShowRendered(pending);
            }
            else if (_documentPath is null)
            {
                ShowRendered(MarkdownHtmlRenderer.Welcome());
                StatusText.Text = "No file open";
            }
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowError(
                "The Microsoft Edge WebView2 Runtime is not installed. SandMarkDown needs it to render Markdown. Install the Evergreen Runtime from Microsoft, then open SandMarkDown again.");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ConfigureViewer()
    {
        var core = Viewer.CoreWebView2;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDefaultScriptDialogsEnabled = false;
        core.Settings.IsSwipeNavigationEnabled = false;
        core.Settings.IsWebMessageEnabled = true;
        core.AddWebResourceRequestedFilter(
            $"https://{MarkdownHtmlRenderer.VirtualHost}/*",
            CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;
        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += OnNavigationCompleted;
        core.WebMessageReceived += OnWebMessageReceived;
        core.NewWindowRequested += OnNewWindowRequested;
    }

    private void ShowRendered(RenderedDocument document)
    {
        _html = document.Html;
        _resources.Clear();
        foreach (var pair in document.LocalResources)
        {
            _resources[pair.Key] = pair.Value;
        }

        if (!_webReady || Viewer.CoreWebView2 is null)
        {
            _pending = document;
            return;
        }

        _pending = null;
        // The preview URL never changes. A query value forces WebView2 to load the new page.
        Viewer.CoreWebView2.Navigate($"{MarkdownHtmlRenderer.PreviewUrl}?d={DateTime.UtcNow.Ticks}");
        Viewer.Focus();
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var environment = Viewer.CoreWebView2.Environment;
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)
            || !uri.Host.Equals(MarkdownHtmlRenderer.VirtualHost, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (uri.AbsolutePath.Equals("/preview", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = Encoding.UTF8.GetBytes(_html);
            e.Response = environment.CreateWebResourceResponse(
                new MemoryStream(bytes),
                200,
                "OK",
                "Content-Type: text/html; charset=utf-8");
            return;
        }

        const string localPrefix = "/local/";
        if (uri.AbsolutePath.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var token = Uri.UnescapeDataString(uri.AbsolutePath[localPrefix.Length..]);
            if (_resources.TryGetValue(token, out var path) && File.Exists(path))
            {
                try
                {
                    e.Response = environment.CreateWebResourceResponse(
                        File.OpenRead(path),
                        200,
                        "OK",
                        "Content-Type: " + ContentType(path));
                    return;
                }
                catch (IOException)
                {
                    // Fall through to the not-found response.
                }
            }
        }

        e.Response = environment.CreateWebResourceResponse(Stream.Null, 404, "Not Found", "Content-Type: text/plain");
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
        {
            e.Cancel = true;
            return;
        }

        if (uri.Host.Equals(MarkdownHtmlRenderer.VirtualHost, StringComparison.OrdinalIgnoreCase))
        {
            if (uri.AbsolutePath.Equals("/preview", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            const string localPrefix = "/local/";
            if (uri.AbsolutePath.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = Uri.UnescapeDataString(uri.AbsolutePath[localPrefix.Length..]);
                if (_resources.TryGetValue(token, out var path))
                {
                    e.Cancel = true;
                    OpenLinkedLocalFile(path, uri.Fragment);
                    return;
                }
            }

            e.Cancel = true;
            return;
        }

        if (uri.Scheme is "http" or "https")
        {
            e.Cancel = true;
            OpenExternal(uri.AbsoluteUri);
            return;
        }

        e.Cancel = true;
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || string.IsNullOrEmpty(_pendingFragment) || Viewer.CoreWebView2 is null)
        {
            return;
        }

        var fragment = JsonSerializer.Serialize(_pendingFragment);
        _pendingFragment = null;
        await Viewer.CoreWebView2.ExecuteScriptAsync($"location.hash = {fragment};");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("type", out var typeProperty))
            {
                return;
            }

            switch (typeProperty.GetString())
            {
                case "zoomIn":
                    SetZoom(ZoomMath.ZoomIn(_zoom));
                    break;
                case "zoomOut":
                    SetZoom(ZoomMath.ZoomOut(_zoom));
                    break;
                case "zoomReset":
                    SetZoom(ZoomMath.Actual);
                    break;
                case "open":
                    ShowOpenDialog();
                    break;
                case "close":
                    CloseDocument();
                    break;
            }
        }
        catch (JsonException)
        {
            // Ignore page messages that are not viewer commands.
        }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (!string.IsNullOrWhiteSpace(e.Uri))
        {
            OpenExternal(e.Uri);
        }
    }

    private void OpenLinkedLocalFile(string path, string fragment)
    {
        if (MarkdownPaths.IsMarkdownFile(path))
        {
            OpenDocument(path, string.IsNullOrEmpty(fragment) ? null : fragment);
            return;
        }

        OpenExternal(path);
    }

    private void CloseDocument()
    {
        _documentPath = null;
        _pendingFragment = null;
        CloseMenuItem.IsEnabled = false;
        StatusText.Text = "No file open";
        Title = Branding.ProductName + Branding.ProductTitleSuffix;
        ShowRendered(MarkdownHtmlRenderer.Welcome());
    }

    private void ShowOpenDialog()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open Markdown",
            Filter = "Markdown (*.md;*.markdown)|*.md;*.markdown|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == true)
        {
            OpenPath(dialog.FileName);
        }
    }

    private void SetZoom(double factor)
    {
        _zoom = factor;
        if (_webReady)
        {
            Viewer.ZoomFactor = factor;
        }

        ZoomText.Text = ZoomMath.FormatPercent(factor);
    }

    private static void OpenExternal(string pathOrUrl)
    {
        try
        {
            Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(ex.Message, Branding.ProductName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, Branding.ProductName, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static string ContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream"
        };
    }

    private void OnOpenClick(object sender, RoutedEventArgs e) => ShowOpenDialog();

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseDocument();

    private void OnExitClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void OnZoomInClick(object sender, RoutedEventArgs e) => SetZoom(ZoomMath.ZoomIn(_zoom));

    private void OnZoomOutClick(object sender, RoutedEventArgs e) => SetZoom(ZoomMath.ZoomOut(_zoom));

    private void OnZoomResetClick(object sender, RoutedEventArgs e) => SetZoom(ZoomMath.Actual);

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Viewer.IsKeyboardFocusWithin || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.O:
                ShowOpenDialog();
                e.Handled = true;
                break;
            case Key.W:
                CloseDocument();
                e.Handled = true;
                break;
            case Key.OemPlus:
            case Key.Add:
                SetZoom(ZoomMath.ZoomIn(_zoom));
                e.Handled = true;
                break;
            case Key.OemMinus:
            case Key.Subtract:
                SetZoom(ZoomMath.ZoomOut(_zoom));
                e.Handled = true;
                break;
            case Key.D0:
            case Key.NumPad0:
                SetZoom(ZoomMath.Actual);
                e.Handled = true;
                break;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        var markdown = files.FirstOrDefault(MarkdownPaths.IsMarkdownFile);
        if (markdown is null)
        {
            StatusText.Text = "Drop a .md or .markdown file.";
            return;
        }

        OpenPath(markdown);
    }
}
