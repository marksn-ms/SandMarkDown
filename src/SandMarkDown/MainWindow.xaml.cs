using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using SandMarkDown.Core;

namespace SandMarkDown;

/// <summary>
/// Reads Markdown files. There is no editor. Back and forward revisit files opened in this window.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly string ZoomSettingsPath = Path.Combine(App.DataFolder, "zoom.json");
    private static readonly string PlacementPath = Path.Combine(App.DataFolder, "window.json");

    private readonly DispatcherTimer _reloadTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private FileSystemWatcher? _watcher;
    private int _reloadAttempts;
    private double? _pendingScroll;
    private readonly Dictionary<string, string> _resources = new(StringComparer.Ordinal);
    private readonly HashSet<string> _remoteImagesShown = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReadingHistory _history = new();
    private RenderedDocument? _pending;
    private string? _documentPath;
    private string? _pendingFragment;
    private string _html = string.Empty;
    private string _contentSecurityPolicy = string.Empty;
    private double _zoom = ZoomMath.Actual;
    private bool _webReady;
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
        RestorePlacement();
        _reloadTimer.Tick += OnReloadTimerTick;
        Closing += (_, _) => SavePlacement();
        Closed += (_, _) => StopWatching();
    }

    /// <summary>
    /// Reads <paramref name="path"/> and shows it. Missing files and read errors are reported in a dialog.
    /// </summary>
    /// <param name="path">Markdown file to open.</param>
    public void OpenPath(string path)
    {
        OpenDocument(path, fragment: null, recordHistory: true);
    }

    private bool OpenDocument(string path, string? fragment, bool recordHistory, bool quiet = false)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                if (!quiet)
                {
                    ShowError($"File not found:\n{fullPath}");
                }

                return false;
            }

            var markdown = File.ReadAllText(fullPath);
            var rendered = MarkdownHtmlRenderer.Render(
                markdown,
                fullPath,
                showRemoteImages: _remoteImagesShown.Contains(fullPath));
            _documentPath = fullPath;
            _pendingFragment = string.IsNullOrEmpty(fragment) ? null : fragment;
            CloseMenuItem.IsEnabled = true;
            StatusText.Text = fullPath;
            Title = $"{Path.GetFileName(fullPath)} - {Branding.ProductName}{Branding.ProductTitleSuffix}";
            ShowRendered(rendered);
            WatchFile(fullPath);
            if (recordHistory)
            {
                _history.Record(fullPath, fragment);
                UpdateHistoryMenu();
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!quiet)
            {
                ShowError(ex.Message);
            }

            return false;
        }
    }

    // Editors often save by writing a temp file and renaming it, so changes, creates, and renames all count.
    private void WatchFile(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (_watcher is not null
            && string.Equals(_watcher.Path, directory, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_watcher.Filter, Path.GetFileName(fullPath), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        StopWatching();
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(directory, Path.GetFileName(fullPath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime
            };
            watcher.Changed += OnWatchedFileChanged;
            watcher.Created += OnWatchedFileChanged;
            watcher.Renamed += OnWatchedFileChanged;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            // Some network locations cannot be watched. The file still reads normally.
        }
    }

    private void StopWatching()
    {
        _reloadTimer.Stop();
        if (_watcher is null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _reloadAttempts = 0;
            _reloadTimer.Stop();
            _reloadTimer.Start();
        });
    }

    private async void OnReloadTimerTick(object? sender, EventArgs e)
    {
        _reloadTimer.Stop();
        if (_documentPath is not { } path)
        {
            return;
        }

        if (!File.Exists(path))
        {
            StatusText.Text = $"{path} was moved or deleted. Showing the last version read.";
            return;
        }

        _pendingScroll = await ReadScrollAsync();
        if (OpenDocument(path, fragment: null, recordHistory: false, quiet: true))
        {
            StatusText.Text = $"{path}  ·  Updated {DateTime.Now:t}";
            return;
        }

        // The editor may still be writing. Try a few more times before giving up quietly.
        _pendingScroll = null;
        if (++_reloadAttempts < 4)
        {
            _reloadTimer.Start();
        }
    }

    private async Task<double?> ReadScrollAsync()
    {
        if (!_webReady || Viewer.CoreWebView2 is null)
        {
            return null;
        }

        try
        {
            var result = await Viewer.CoreWebView2.ExecuteScriptAsync("window.scrollY");
            return double.TryParse(result, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) ? y : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private void RestorePlacement()
    {
        var saved = WindowPlacement.Load(PlacementPath);
        if (saved is null)
        {
            return;
        }

        var fitted = saved.FitTo(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight,
            MinWidth,
            MinHeight);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = fitted.Left;
        Top = fitted.Top;
        Width = fitted.Width;
        Height = fitted.Height;
        if (fitted.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SavePlacement()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (bounds.IsEmpty)
        {
            return;
        }

        try
        {
            new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, WindowState == WindowState.Maximized)
                .Save(PlacementPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The window opens centered next time instead.
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
            var environment = await CoreWebView2Environment.CreateAsync(null, App.WebViewDataFolder);
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
        _contentSecurityPolicy = document.ContentSecurityPolicy;
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
        // A new query value forces WebView2 to load the page. WebView2's own back and forward
        // are cancelled in OnNavigationStarting, because file history belongs to ReadingHistory.
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
                "Content-Type: text/html; charset=utf-8\r\n"
                + "Content-Security-Policy: " + _contentSecurityPolicy + "\r\n"
                + "X-Content-Type-Options: nosniff\r\n"
                + "Referrer-Policy: no-referrer");
            return;
        }

        // Local files reach the page only as images. Anything else would let markup read it.
        const string localPrefix = "/local/";
        if (e.ResourceContext == CoreWebView2WebResourceContext.Image
            && uri.AbsolutePath.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var token = Uri.UnescapeDataString(uri.AbsolutePath[localPrefix.Length..]);
            if (_resources.TryGetValue(token, out var path)
                && MarkdownPaths.ImageContentType(path) is string contentType
                && File.Exists(path))
            {
                try
                {
                    e.Response = environment.CreateWebResourceResponse(
                        File.OpenRead(path),
                        200,
                        "OK",
                        "Content-Type: " + contentType + "\r\nX-Content-Type-Options: nosniff");
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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

        if (uri.Host.Equals(MarkdownHtmlRenderer.VirtualHost, StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.Equals("/preview", StringComparison.OrdinalIgnoreCase))
        {
            if (e.NavigationKind == CoreWebView2NavigationKind.BackOrForward)
            {
                e.Cancel = true;
            }

            return;
        }

        e.Cancel = true;
        if (e.IsUserInitiated)
        {
            FollowLink(uri);
        }
    }

    // Links open only from a click. A file cannot open a browser or another program on its own.
    private void FollowLink(Uri uri)
    {
        if (uri.Host.Equals(MarkdownHtmlRenderer.VirtualHost, StringComparison.OrdinalIgnoreCase))
        {
            const string localPrefix = "/local/";
            if (uri.AbsolutePath.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = Uri.UnescapeDataString(uri.AbsolutePath[localPrefix.Length..]);
                if (_resources.TryGetValue(token, out var path))
                {
                    OpenLinkedLocalFile(path, uri.Fragment);
                }
            }

            return;
        }

        if (uri.Scheme is "http" or "https" or "mailto")
        {
            OpenExternal(uri.AbsoluteUri);
        }
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || Viewer.CoreWebView2 is null)
        {
            return;
        }

        if (_pendingScroll is double y)
        {
            _pendingScroll = null;
            var top = y.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await Viewer.CoreWebView2.ExecuteScriptAsync($"window.scrollTo(0, {top});");
            return;
        }

        if (string.IsNullOrEmpty(_pendingFragment))
        {
            return;
        }

        var fragment = JsonSerializer.Serialize(_pendingFragment);
        _pendingFragment = null;
        await Viewer.CoreWebView2.ExecuteScriptAsync($"location.hash = {fragment};");
    }

    private void PrintDocument()
    {
        if (!_webReady || Viewer.CoreWebView2 is null)
        {
            return;
        }

        Viewer.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
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
                case "showRemoteImages":
                    ShowRemoteImages();
                    break;
                case "print":
                    PrintDocument();
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
        if (e.IsUserInitiated && Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
        {
            FollowLink(uri);
        }
    }

    private void ShowRemoteImages()
    {
        if (_documentPath is null)
        {
            return;
        }

        _remoteImagesShown.Add(_documentPath);
        OpenDocument(_documentPath, fragment: null, recordHistory: false);
    }

    private void OpenLinkedLocalFile(string path, string fragment)
    {
        if (MarkdownPaths.IsMarkdownFile(path))
        {
            OpenDocument(path, string.IsNullOrEmpty(fragment) ? null : fragment, recordHistory: true);
            return;
        }

        if (Directory.Exists(path))
        {
            OpenExternal(path);
            return;
        }

        if (!File.Exists(path))
        {
            StatusText.Text = $"Link target not found: {path}";
            return;
        }

        // A link to a program or script is shown in File Explorer instead of being run.
        if (ShellFiles.CanRunCode(path))
        {
            ShellFiles.ShowInFolder(path);
            StatusText.Text = $"Showing {Path.GetFileName(path)} in File Explorer. SandMarkDown does not run programs from links.";
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
        StopWatching();
        _documentPath = null;
        _pendingFragment = null;
        _pendingScroll = null;
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

    private void OnOpenClick(object sender, RoutedEventArgs e) => ShowOpenDialog();

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseDocument();

    private void OnPrintClick(object sender, RoutedEventArgs e) => PrintDocument();

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
            case Key.P:
                PrintDocument();
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
