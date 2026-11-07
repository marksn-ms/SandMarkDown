using System.Windows;
using System.Windows.Input;
using SandMarkDown.Core;
using Velopack;
using Velopack.Sources;

namespace SandMarkDown;

/// <summary>
/// Checks the public GitHub Releases feed and installs a newer version when the user agrees.
/// A copy that was not installed with Setup does not contact GitHub.
/// </summary>
internal static class AppUpdate
{
    /// <summary>
    /// When set to a directory, the update check reads that folder instead of GitHub.
    /// The directory must contain <c>releases.win.json</c>. Used to try an update on one PC.
    /// </summary>
    internal const string LocalFeedVariable = "SANDMARKDOWN_UPDATE_PATH";

    private static int _busy;

    /// <summary>
    /// Offers a newer installed version. Startup passes <paramref name="interactive"/> false so an
    /// offline PC or a current version stays quiet. Help, Check for updates passes true.
    /// </summary>
    /// <param name="owner">Window that owns the prompt.</param>
    /// <param name="interactive">True when the user asked to check.</param>
    /// <param name="reopenPath">File to open again after the update restarts the reader.</param>
    internal static async Task OfferAsync(Window owner, bool interactive, string? reopenPath)
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }

        try
        {
            var manager = CreateManager();
            try
            {
                if (!manager.IsInstalled)
                {
                    if (interactive)
                    {
                        Inform(
                            owner,
                            "Updates are available after SandMarkDown is installed with its Setup program. A copy started from a build folder does not check GitHub.");
                    }

                    return;
                }

                var update = await manager.CheckForUpdatesAsync().ConfigureAwait(true);
                if (update is null)
                {
                    if (interactive)
                    {
                        Inform(owner, $"You have the latest version ({manager.CurrentVersion}).");
                    }

                    return;
                }

                if (!CanPrompt(owner))
                {
                    return;
                }

                var target = update.TargetFullRelease.Version.ToString();
                var answer = MessageBox.Show(
                    owner,
                    $"{Branding.ProductName} {target} is available. SandMarkDown will close and restart to install it.\n\nYour files and reading settings stay on this PC.",
                    Branding.ProductName,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (answer != MessageBoxResult.Yes)
                {
                    return;
                }

                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    await manager.DownloadUpdatesAsync(update).ConfigureAwait(true);
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }

                var restartArgs = string.IsNullOrWhiteSpace(reopenPath) ? null : new[] { reopenPath };
                manager.ApplyUpdatesAndRestart(update, restartArgs);
            }
            finally
            {
                (manager as IDisposable)?.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (interactive)
            {
                Inform(owner, "Could not check for updates. " + ex.Message);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private static UpdateManager CreateManager()
    {
        var local = Environment.GetEnvironmentVariable(LocalFeedVariable);
        if (!string.IsNullOrWhiteSpace(local))
        {
            return new UpdateManager(local);
        }

        return new UpdateManager(new GithubSource(Branding.SourceUrl, accessToken: null, prerelease: false));
    }

    private static bool CanPrompt(Window owner)
    {
        try
        {
            return owner.IsVisible;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void Inform(Window owner, string message)
    {
        if (!CanPrompt(owner))
        {
            return;
        }

        MessageBox.Show(owner, message, Branding.ProductName, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
