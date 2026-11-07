using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using SandMarkDown.Core;

namespace SandMarkDown;

/// <summary>
/// Registers SandMarkDown for Markdown files for the current user and opens Settings so the user can make
/// it the default. Windows does not let an app set itself as the default.
/// </summary>
internal static class FileAssociation
{
    private const int AppModelErrorNoPackage = 15700;
    private const int ErrorInsufficientBuffer = 122;

    /// <summary>
    /// True when running from the Store package, whose manifest already declares the file types.
    /// </summary>
    public static bool IsPackaged => PackageFamilyName() is not null;

    /// <summary>
    /// The SandMarkDown.exe this process belongs to. Under <c>dotnet SandMarkDown.dll</c> the process is dotnet.exe,
    /// so the program next to the app's files is preferred.
    /// </summary>
    public static string ExecutablePath
    {
        get
        {
            var beside = Path.Combine(AppContext.BaseDirectory, "SandMarkDown.exe");
            return File.Exists(beside) ? beside : Environment.ProcessPath ?? beside;
        }
    }

    /// <summary>
    /// Writes the per-user registration for <see cref="ExecutablePath"/>.
    /// </summary>
    public static void Register()
    {
        foreach (var entry in AssociationPlan.For(ExecutablePath))
        {
            using var key = Registry.CurrentUser.CreateSubKey(entry.SubKey, writable: true);
            if (entry.Value is null)
            {
                key.SetValue(entry.Name, Array.Empty<byte>(), RegistryValueKind.None);
            }
            else
            {
                key.SetValue(entry.Name, entry.Value, RegistryValueKind.String);
            }
        }

        NotifyShell();
    }

    /// <summary>
    /// Registers this copy. Failures are swallowed so a Velopack install or update hook can finish.
    /// The hook has a few seconds and must not show a window.
    /// </summary>
    public static void TryRegister()
    {
        try
        {
            Register();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Help, Set as default can still register later.
        }
    }

    /// <summary>
    /// Removes the per-user registration when it points at this copy.
    /// Settings, recent files, and the error log are left in place.
    /// </summary>
    public static void TryUnregisterThisCopy()
    {
        try
        {
            UnregisterThisCopy();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Uninstall continues. A stale Open with entry can be replaced later.
        }
    }

    /// <summary>
    /// Removes registry entries that point at <see cref="ExecutablePath"/>.
    /// Does not delete <see cref="App.DataFolder"/> and does not change the user's saved default app.
    /// </summary>
    public static void UnregisterThisCopy()
    {
        var mine = Path.GetFullPath(ExecutablePath);
        var registered = RegisteredExecutable();
        if (registered is null || !string.Equals(Path.GetFullPath(registered), mine, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RemoveOpenWith(AssociationPlan.Extensions);
        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{AssociationPlan.ProgId}", throwOnMissingSubKey: false);

        if (CommandPointsAt(mine, @"Software\Classes\Applications\SandMarkDown.exe\shell\open\command"))
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Applications\SandMarkDown.exe", throwOnMissingSubKey: false);
        }

        if (ValuePointsAt(mine, @"Software\Microsoft\Windows\CurrentVersion\App Paths\SandMarkDown.exe"))
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                @"Software\Microsoft\Windows\CurrentVersion\App Paths\SandMarkDown.exe",
                throwOnMissingSubKey: false);
        }

        using (var registeredApps = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", writable: true))
        {
            var capabilities = registeredApps?.GetValue(AssociationPlan.RegisteredName) as string;
            if (string.Equals(capabilities, AssociationPlan.CapabilitiesKey, StringComparison.OrdinalIgnoreCase))
            {
                registeredApps!.DeleteValue(AssociationPlan.RegisteredName, throwOnMissingValue: false);
            }
        }

        Registry.CurrentUser.DeleteSubKeyTree(AssociationPlan.CapabilitiesKey, throwOnMissingSubKey: false);
        NotifyShell();
    }

    /// <summary>
    /// Re-registers when the registered program no longer exists, for example after the folder moved.
    /// A registration pointing at another copy that still exists is left alone.
    /// </summary>
    public static void RepairIfMoved()
    {
        if (IsPackaged)
        {
            return;
        }

        try
        {
            var registered = RegisteredExecutable();
            if (registered is not null && !File.Exists(registered))
            {
                Register();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Help, Set as default can still register later.
        }
    }

    /// <summary>
    /// Program the SandMarkDown file type currently opens, or <see langword="null"/> when not registered.
    /// </summary>
    public static string? RegisteredExecutable()
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{AssociationPlan.ProgId}\shell\open\command");
        return AssociationPlan.ExecutableFromCommand(key?.GetValue(string.Empty) as string);
    }

    /// <summary>
    /// True when the user's saved choice for <c>.md</c> is SandMarkDown.
    /// </summary>
    public static bool IsUserDefault()
    {
        const string fileExts = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.md";
        using var choice = Registry.CurrentUser.OpenSubKey(fileExts + @"\UserChoice");
        using var latest = Registry.CurrentUser.OpenSubKey(fileExts + @"\UserChoiceLatest\ProgId");
        var progId = choice?.GetValue("ProgId") as string ?? latest?.GetValue("ProgId") as string;
        return string.Equals(progId, AssociationPlan.ProgId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Opens Settings, Default apps, on SandMarkDown's page.
    /// </summary>
    public static void OpenDefaultAppsSettings()
    {
        var family = PackageFamilyName();
        var uri = family is null
            ? AssociationPlan.SettingsUri
            : $"ms-settings:defaultapps?registeredAUMID={family}!SandMarkDown";
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
        }
    }

    private static string? PackageFamilyName()
    {
        var length = 0;
        var result = GetCurrentPackageFamilyName(ref length, null);
        if (result == AppModelErrorNoPackage || result != ErrorInsufficientBuffer)
        {
            return null;
        }

        var name = new StringBuilder(length);
        return GetCurrentPackageFamilyName(ref length, name) == 0 ? name.ToString() : null;
    }

    private static void RemoveOpenWith(IReadOnlyList<string> extensions)
    {
        foreach (var extension in extensions)
        {
            using var openWith = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{extension}\OpenWithProgids", writable: true);
            openWith?.DeleteValue(AssociationPlan.ProgId, throwOnMissingValue: false);
        }
    }

    private static bool CommandPointsAt(string executablePath, string subKey)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey);
        var command = AssociationPlan.ExecutableFromCommand(key?.GetValue(string.Empty) as string);
        return command is not null && string.Equals(Path.GetFullPath(command), executablePath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValuePointsAt(string executablePath, string subKey)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey);
        var value = key?.GetValue(string.Empty) as string;
        return value is not null && string.Equals(Path.GetFullPath(value), executablePath, StringComparison.OrdinalIgnoreCase);
    }

    private static void NotifyShell() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref int packageFamilyNameLength, StringBuilder? packageFamilyName);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
