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
/// Reads Markdown files. There is no editor. Back and forward revisit files opened in this window.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly string ZoomSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Sandmark Technologies",
        "SandMarkDown",
        "zoom.json");

    private readonly Dictionary<string, string> _resources = new(StringComparer.Ordinal);
    private readonly ReadingHistory _history = new();
    private RenderedDocument? _pending;
    private string? _documentPath;
    private string? _pendingFragment;
    private string _html = string.Empty;
    private double _zoom = ZoomMath.Actual;
    private bool _webReady;
    private bool _previewLoaded;
    private long _lastTravelMs;

    /// <summary>
    /// Initializes the reader window.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        Title = Branding.ProductName + Branding.ProductTitleSuffix;
        _zoom = ZoomPreferences.Load(ZoomSettingsPath);
        ZoomText.Text = ZoomMath.FormatPercent(_zoom);
    }

    /// <summary>
    /// Reads <paramref name="path"/> and shows it. Missing files and read errors are reported in a dialog.
    /// </summary>
    /// <param name="path">Markdown file to open.</param>
    public void OpenPath(string path)
    {
        OpenDocument(path, fragment: null, recordHistory: true);
    }

    private bool OpenDocument(string path, string? fragment, bool recordHistory)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                ShowError($"File not found:\n{fullPath}");
                return false;
            }

            var markdown = File.ReadAllText(fullPath);
            var rendered = MarkdownHtmlRenderer.Render(markdown, fullPath);
            _documentPath = fullPath;
            _pendingFragment = string.IsNullOrEmpty(fragment) ? null : fragment;
            CloseMenuItem.IsEnabled = true;
            StatusText.Text = fullPath;
            Title = $"{Path.GetFileName(fullPath)} - {Branding.ProductName}{Branding.ProductTitleSuffix}";
            ShowRendered(rendered);
            if (recordHistory)
            {
                _history.Record(fullPath, fragment);
                UpdateHistoryMenu();
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
            return false;
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
        // A new query value forces WebView2 to load the page. Later loads replace the
        // current history entry so the mouse back button does not walk WebView2's list.
        // File history belongs to ReadingHistory.
        var url = $"{MarkdownHtmlRenderer.PreviewUrl}?d={DateTime.UtcNow.Ticks}";
        if (!_previewLoaded)
        {
            _previewLoaded = true;
            Viewer.CoreWebView2.Navigate(url);
        }
        else
        {
            _ = ReplacePreviewAsync(url);
        }

        Viewer.Focus();
    }

    private async Task ReplacePreviewAsync(string url)
    {
        var core = Viewer.CoreWebView2;
        if (core is null)
        {
            return;
        }

        try
        {
            await core.ExecuteScriptAsync($"location.replace({JsonSerializer.Serialize(url)})");
        }
        catch (InvalidOperationException)
        {
            // The viewer closed while the page was loading.
        }
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
                if (e.NavigationKind == CoreWebView2NavigationKind.BackOrForward)
                {
                    e.Cancel = true;
                }

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
                case "back":
                    GoBack();
                    break;
                case "forward":
                    GoForward();
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
            OpenDocument(path, string.IsNullOrEmpty(fragment) ? null : fragment, recordHistory: true);
            return;
        }

        OpenExternal(path);
    }

    private void CloseDocument()
    {
        if (_documentPath is null)
        {
            return;
        }

        ShowWelcome();
        _history.Record(null, null);
        UpdateHistoryMenu();
    }

    private void ShowWelcome()
    {
        _documentPath = null;
        _pendingFragment = null;
        CloseMenuItem.IsEnabled = false;
        StatusText.Text = "No file open";
        Title = Branding.ProductName + Branding.ProductTitleSuffix;
        ShowRendered(MarkdownHtmlRenderer.Welcome());
    }

    private void GoBack()
    {
        if (!TravelAllowed())
        {
            return;
        }

        var place = _history.GoBack();
        if (place is null)
        {
            return;
        }

        if (!ShowPlace(place.Value))
        {
            _history.GoForward();
        }

        UpdateHistoryMenu();
    }

    private void GoForward()
    {
        if (!TravelAllowed())
        {
            return;
        }

        var place = _history.GoForward();
        if (place is null)
        {
            return;
        }

        if (!ShowPlace(place.Value))
        {
            _history.GoBack();
        }

        UpdateHistoryMenu();
    }

    private bool ShowPlace(HistoryPlace place)
    {
        if (place.Path is null)
        {
            ShowWelcome();
            return true;
        }

        return OpenDocument(place.Path, place.Fragment, recordHistory: false);
    }

    private bool TravelAllowed()
    {
        var now = Environment.TickCount64;
        if (now - _lastTravelMs < 200)
        {
            return false;
        }

        _lastTravelMs = now;
        return true;
    }

    private void UpdateHistoryMenu()
    {
        BackMenuItem.IsEnabled = _history.CanGoBack;
        ForwardMenuItem.IsEnabled = _history.CanGoForward;
    }

    private void OpenFind()
    {
        if (!_webReady || Viewer.CoreWebView2 is null)
        {
            return;
        }

        _ = Viewer.CoreWebView2.ExecuteScriptAsync("window.smtFind && window.smtFind.open()");
    }

    private void StepFind(bool backward)
    {
        if (!_webReady || Viewer.CoreWebView2 is null)
        {
            return;
        }

        var call = backward ? "window.smtFind.previous()" : "window.smtFind.next()";
        _ = Viewer.CoreWebView2.ExecuteScriptAsync($"window.smtFind && {call}");
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
        try
        {
            ZoomPreferences.Save(ZoomSettingsPath, factor);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The page is already zoomed. Remembering it can wait for the next change.
        }
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

    private void OnBackClick(object sender, RoutedEventArgs e) => GoBack();

    private void OnForwardClick(object sender, RoutedEventArgs e) => GoForward();

    private void OnFindClick(object sender, RoutedEventArgs e) => OpenFind();

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
        if (Viewer.IsKeyboardFocusWithin)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Left)
            {
                GoBack();
                e.Handled = true;
            }
            else if (key == Key.Right)
            {
                GoForward();
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.F3 && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            StepFind(backward: Keyboard.Modifiers == ModifierKeys.Shift);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.F:
                OpenFind();
                e.Handled = true;
                break;
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
