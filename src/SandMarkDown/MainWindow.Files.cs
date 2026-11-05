using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SandMarkDown.Core;

namespace SandMarkDown;

/// <summary>
/// File, Open Recent and the Explorer sidebar.
/// </summary>
public partial class MainWindow
{
    private const string FolderGlyph = "\uE8B7";
    private const string FileGlyph = "\uE8A5";

    private static readonly string RecentPath = Path.Combine(App.DataFolder, "recent.json");

    private readonly RecentFiles _recent = RecentFiles.Load(RecentPath);
    private string? _explorerRoot;
    private bool _explorerPinned;

    private void InitializeFilesUi()
    {
        RebuildRecentMenu();
        ExplorerMenuItem.IsChecked = _view.ShowExplorer;
        ApplyExplorerVisibility();
    }

    private void RememberRecent(string fullPath)
    {
        _recent.Add(fullPath);
        try
        {
            _recent.Save(RecentPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The menu still shows it for this session.
        }

        RebuildRecentMenu();

        // Also lists the file under Recent when SandMarkDown's taskbar button is right-clicked.
        System.Windows.Shell.JumpList.AddToRecentCategory(fullPath);
    }

    private void RebuildRecentMenu()
    {
        RecentMenuItem.Items.Clear();
        if (_recent.Items.Count == 0)
        {
            RecentMenuItem.Items.Add(new MenuItem { Header = "No recent files", IsEnabled = false });
            return;
        }

        for (var i = 0; i < _recent.Items.Count; i++)
        {
            var path = _recent.Items[i];
            var item = new MenuItem { Header = _recent.MenuText(i), Tag = path, ToolTip = path };
            item.Click += OnRecentClick;
            RecentMenuItem.Items.Add(item);
        }

        RecentMenuItem.Items.Add(new Separator());
        var clear = new MenuItem { Header = "_Clear Recently Opened" };
        clear.Click += (_, _) =>
        {
            _recent.Clear();
            try
            {
                _recent.Save(RecentPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Cleared for this session.
            }

            RebuildRecentMenu();
        };
        RecentMenuItem.Items.Add(clear);
    }

    private void OnRecentClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path })
        {
            return;
        }

        if (!File.Exists(path))
        {
            _recent.Remove(path);
            try
            {
                _recent.Save(RecentPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Removed for this session.
            }

            RebuildRecentMenu();
            StatusText.Text = $"{path} no longer exists. It was removed from Open Recent.";
            return;
        }

        OpenPath(path);
    }

    private void ApplyExplorerVisibility()
    {
        var show = _view.ShowExplorer;
        ExplorerPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ExplorerSplitter.Visibility = ExplorerPanel.Visibility;
        ExplorerColumn.MinWidth = show ? ViewSettings.MinExplorerWidth : 0;
        ExplorerColumn.MaxWidth = show ? ViewSettings.MaxExplorerWidth : double.PositiveInfinity;
        ExplorerColumn.Width = show ? new GridLength(_view.ExplorerWidth) : new GridLength(0);
        if (!show)
        {
            return;
        }

        if (_explorerRoot is null && _documentPath is not null)
        {
            SetExplorerRoot(Path.GetDirectoryName(_documentPath), pinned: false);
        }
        else
        {
            LoadExplorer();
        }
    }

    // A folder opened with Open Folder stays put. Otherwise the Explorer follows the open file's folder.
    private void FollowDocumentInExplorer(string fullPath)
    {
        var folder = Path.GetDirectoryName(fullPath);
        var outside = _explorerRoot is null || MarkdownFolder.PathFromRoot(_explorerRoot, fullPath) is null;
        if (outside && !_explorerPinned)
        {
            SetExplorerRoot(folder, pinned: false);
            return;
        }

        if (_view.ShowExplorer)
        {
            RevealInExplorer(fullPath);
        }
    }

    private void SetExplorerRoot(string? folder, bool pinned)
    {
        _explorerRoot = string.IsNullOrEmpty(folder) ? null : Path.GetFullPath(folder);
        _explorerPinned = pinned && _explorerRoot is not null;
        LoadExplorer();
    }

    private void LoadExplorer()
    {
        if (!_view.ShowExplorer)
        {
            return;
        }

        ExplorerTree.Items.Clear();
        if (_explorerRoot is null)
        {
            ExplorerFolderText.Text = string.Empty;
            ExplorerEmptyText.Text = "Open a file or a folder to browse its Markdown files.";
            ExplorerEmptyText.Visibility = Visibility.Visible;
            return;
        }

        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(_explorerRoot));
        ExplorerFolderText.Text = string.IsNullOrEmpty(name) ? _explorerRoot : name;
        ExplorerFolderText.ToolTip = _explorerRoot;
        var added = AddChildren(ExplorerTree.Items, _explorerRoot);
        ExplorerEmptyText.Text = "No Markdown files in this folder.";
        ExplorerEmptyText.Visibility = added == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_documentPath is not null)
        {
            RevealInExplorer(_documentPath);
        }
    }

    private static int AddChildren(ItemCollection items, string folder)
    {
        var entries = MarkdownFolder.List(folder);
        foreach (var entry in entries)
        {
            items.Add(CreateExplorerItem(entry));
        }

        return entries.Count;
    }

    private static TreeViewItem CreateExplorerItem(FolderEntry entry)
    {
        var glyph = new TextBlock
        {
            Text = entry.IsFolder ? FolderGlyph : FileGlyph,
            FontFamily = (FontFamily)Application.Current.Resources["SmtIcons"],
            Margin = new Thickness(0, 1, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        glyph.SetResourceReference(TextBlock.ForegroundProperty, entry.IsFolder ? "SmtAccent" : "SmtMuted");

        var label = new TextBlock { Text = entry.Name, VerticalAlignment = VerticalAlignment.Center };
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(glyph);
        header.Children.Add(label);

        var item = new TreeViewItem { Header = header, Tag = entry, ToolTip = entry.FullPath };
        System.Windows.Automation.AutomationProperties.SetName(item, entry.IsFolder ? $"Folder {entry.Name}" : entry.Name);
        if (entry.IsFolder)
        {
            // A stand-in child shows the expand arrow. The real children load on first expand.
            item.Items.Add(string.Empty);
            item.Expanded += OnExplorerFolderExpanded;
        }

        return item;
    }

    private static void OnExplorerFolderExpanded(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, e.OriginalSource) || sender is not TreeViewItem { Tag: FolderEntry entry } item)
        {
            return;
        }

        if (item.Items.Count == 1 && item.Items[0] is string)
        {
            item.Items.Clear();
            AddChildren(item.Items, entry.FullPath);
        }
    }

    private void RevealInExplorer(string fullPath)
    {
        if (_explorerRoot is null || MarkdownFolder.PathFromRoot(_explorerRoot, fullPath) is not { } folders)
        {
            return;
        }

        var items = ExplorerTree.Items;
        foreach (var folder in folders)
        {
            var folderItem = FindItem(items, folder);
            if (folderItem is null)
            {
                return;
            }

            folderItem.IsExpanded = true;
            items = folderItem.Items;
        }

        if (FindItem(items, fullPath) is { } fileItem)
        {
            fileItem.IsSelected = true;
            fileItem.BringIntoView();
        }
    }

    private static TreeViewItem? FindItem(ItemCollection items, string path) =>
        items.OfType<TreeViewItem>().FirstOrDefault(item =>
            item.Tag is FolderEntry entry && string.Equals(entry.FullPath, path, StringComparison.OrdinalIgnoreCase));

    private void OnExplorerClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || FindAncestor<ToggleButton>(source) is not null)
        {
            return;
        }

        if (FindAncestor<TreeViewItem>(source) is { Tag: FolderEntry entry } item)
        {
            ActivateExplorerItem(item, entry);
        }
    }

    private void OnExplorerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ExplorerTree.SelectedItem is TreeViewItem { Tag: FolderEntry entry } item)
        {
            ActivateExplorerItem(item, entry);
            e.Handled = true;
        }
    }

    private void ActivateExplorerItem(TreeViewItem item, FolderEntry entry)
    {
        if (entry.IsFolder)
        {
            item.IsExpanded = !item.IsExpanded;
            return;
        }

        if (!string.Equals(entry.FullPath, _documentPath, StringComparison.OrdinalIgnoreCase))
        {
            OpenPath(entry.FullPath);
        }
    }

    private void OnExplorerRefreshClick(object sender, RoutedEventArgs e) => LoadExplorer();

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Open Folder",
            InitialDirectory = _explorerRoot
                ?? (_documentPath is null ? null : Path.GetDirectoryName(_documentPath))
                ?? string.Empty
        };
        if (dialog.ShowDialog(this) == true)
        {
            OpenFolder(dialog.FolderName);
        }
    }

    private void OpenFolder(string folder)
    {
        if (!_view.ShowExplorer)
        {
            ExplorerMenuItem.IsChecked = true;
            ApplyView(_view with { ShowExplorer = true });
        }

        SetExplorerRoot(folder, pinned: true);
    }

    private void ToggleExplorer()
    {
        ExplorerMenuItem.IsChecked = !ExplorerMenuItem.IsChecked;
        ApplyView(_view with { ShowExplorer = ExplorerMenuItem.IsChecked });
    }

    private void OnExplorerResized(object sender, DragCompletedEventArgs e)
    {
        var width = Math.Clamp(ExplorerColumn.ActualWidth, ViewSettings.MinExplorerWidth, ViewSettings.MaxExplorerWidth);
        _view = _view with { ExplorerWidth = width };
        SaveView();
    }

    private static T? FindAncestor<T>(DependencyObject? node)
        where T : DependencyObject
    {
        while (node is not null and not TreeView)
        {
            if (node is T match)
            {
                return match;
            }

            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }

        return null;
    }
}
