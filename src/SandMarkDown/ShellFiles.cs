using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace SandMarkDown;

/// <summary>
/// Decides how a linked local file may be opened.
/// </summary>
internal static class ShellFiles
{
    // Types Windows can run or that redirect to something runnable, beyond what AssocIsDangerous reports.
    private static readonly HashSet<string> ExtraRunnable = new(StringComparer.OrdinalIgnoreCase)
    {
        ".lnk", ".url", ".appref-ms", ".application", ".appinstaller", ".appx", ".appxbundle",
        ".msix", ".msixbundle", ".library-ms", ".search-ms", ".searchconnector-ms",
        ".settingcontent-ms", ".ps1", ".psm1", ".psd1", ".ps1xml", ".hta", ".js", ".jse",
        ".vbs", ".vbe", ".wsf", ".wsh", ".bat", ".cmd", ".com", ".cpl", ".exe", ".scr",
        ".msi", ".msp", ".reg", ".inf", ".jar", ".chm", ".diagcab", ".xll", ".iso", ".img", ".vhd", ".vhdx"
    };

    /// <summary>
    /// Returns whether opening <paramref name="path"/> could run code.
    /// </summary>
    public static bool CanRunCode(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        return ExtraRunnable.Contains(extension) || AssocIsDangerous(extension);
    }

    /// <summary>
    /// Opens File Explorer with <paramref name="path"/> selected.
    /// </summary>
    public static void ShowInFolder(string path)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false });
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssocIsDangerous(string pszAssoc);
}
