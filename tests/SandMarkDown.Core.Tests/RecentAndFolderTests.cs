using SandMarkDown.Core;

namespace SandMarkDown.Core.Tests;

/// <summary>
/// Verifies File, Open Recent and the Explorer folder listing.
/// </summary>
public class RecentAndFolderTests
{
    /// <summary>
    /// Reopening a file moves it to the top without duplicating it.
    /// </summary>
    [Fact]
    public void Recent_moves_a_reopened_file_to_the_top()
    {
        var recent = new RecentFiles();
        recent.Add(@"C:\notes\a.md");
        recent.Add(@"C:\notes\b.md");
        recent.Add(@"C:\NOTES\A.md");

        Assert.Equal(2, recent.Items.Count);
        Assert.Equal(@"C:\NOTES\A.md", recent.Items[0]);
    }

    /// <summary>
    /// The list keeps only the newest files.
    /// </summary>
    [Fact]
    public void Recent_keeps_the_newest_files()
    {
        var recent = new RecentFiles();
        for (var i = 0; i < RecentFiles.Capacity + 3; i++)
        {
            recent.Add($@"C:\notes\{i}.md");
        }

        Assert.Equal(RecentFiles.Capacity, recent.Items.Count);
        Assert.EndsWith($"{RecentFiles.Capacity + 2}.md", recent.Items[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// The list round-trips in order, and a broken file gives an empty list.
    /// </summary>
    [Fact]
    public void Recent_round_trips_in_order()
    {
        var path = Path.Combine(TempDirectory(), "recent.json");
        var recent = new RecentFiles();
        recent.Add(@"C:\notes\old.md");
        recent.Add(@"C:\notes\new.md");
        recent.Save(path);

        var loaded = RecentFiles.Load(path);
        Assert.Equal(recent.Items, loaded.Items);

        File.WriteAllText(path, "{ broken");
        Assert.Empty(RecentFiles.Load(path).Items);
    }

    /// <summary>
    /// Menu text numbers the entry and escapes underscores so they are not read as access keys.
    /// </summary>
    [Fact]
    public void Recent_menu_text_numbers_and_escapes()
    {
        var recent = new RecentFiles();
        recent.Add(@"C:\my_docs\read_me.md");
        Assert.Equal(@"_1  read__me.md  (C:\my__docs)", recent.MenuText(0));
    }

    /// <summary>
    /// The Explorer lists folders, then Markdown files, and skips hidden, dot, and build folders.
    /// </summary>
    [Fact]
    public void Folder_lists_folders_then_markdown_files()
    {
        var root = TempDirectory();
        Directory.CreateDirectory(Path.Combine(root, "guides"));
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        Directory.CreateDirectory(Path.Combine(root, "node_modules"));
        var hidden = Directory.CreateDirectory(Path.Combine(root, "secret"));
        hidden.Attributes |= FileAttributes.Hidden;
        File.WriteAllText(Path.Combine(root, "b.md"), "b");
        File.WriteAllText(Path.Combine(root, "A.markdown"), "a");
        File.WriteAllText(Path.Combine(root, "notes.txt"), "skip");

        var entries = MarkdownFolder.List(root);

        Assert.Equal(["guides", "A.markdown", "b.md"], entries.Select(e => e.Name));
        Assert.True(entries[0].IsFolder);
        Assert.False(entries[1].IsFolder);
    }

    /// <summary>
    /// A missing folder lists nothing instead of failing.
    /// </summary>
    [Fact]
    public void Folder_missing_lists_nothing()
    {
        Assert.Empty(MarkdownFolder.List(Path.Combine(TempDirectory(), "gone")));
    }

    /// <summary>
    /// The path from the root lists each folder to expand on the way to a file.
    /// </summary>
    [Fact]
    public void Path_from_root_lists_folders_to_expand()
    {
        var path = MarkdownFolder.PathFromRoot(@"C:\docs", @"C:\docs\guides\setup\install.md");
        Assert.Equal([@"C:\docs\guides", @"C:\docs\guides\setup"], path);

        Assert.Empty(MarkdownFolder.PathFromRoot(@"C:\docs\", @"C:\docs\readme.md")!);
        Assert.Null(MarkdownFolder.PathFromRoot(@"C:\docs", @"C:\other\readme.md"));
        Assert.Null(MarkdownFolder.PathFromRoot(@"C:\docs", @"D:\docs\readme.md"));
    }

    /// <summary>
    /// Settings saved before the Explorer existed load with the Explorer off, and widths are kept in range.
    /// </summary>
    [Fact]
    public void Older_view_settings_load_with_explorer_defaults()
    {
        var path = Path.Combine(TempDirectory(), "view.json");
        File.WriteAllText(path, """{ "showPreview": false, "showSidebar": true, "darkMode": true }""");
        var loaded = ViewSettings.Load(path);
        Assert.False(loaded.ShowPreview);
        Assert.False(loaded.ShowExplorer);
        Assert.Equal(260, loaded.ExplorerWidth);

        File.WriteAllText(path, """{ "showExplorer": true, "explorerWidth": 5000 }""");
        Assert.Equal(ViewSettings.MaxExplorerWidth, ViewSettings.Load(path).ExplorerWidth);
    }

    private static string TempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sandmarkdown-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
