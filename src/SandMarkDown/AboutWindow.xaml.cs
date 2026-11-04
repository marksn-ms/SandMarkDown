using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SandMarkDown.Core;

namespace SandMarkDown;

/// <summary>
/// About dialog showing product identity and Sandmark Technologies branding.
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>
    /// Initializes the About dialog.
    /// </summary>
    public AboutWindow()
    {
        InitializeComponent();
        Title = $"{Branding.ProductName}{Branding.ProductTitleSuffix}";
        ProductNameText.Text = Branding.ProductName;
        CompanyText.Text = Branding.CompanyName;
        DescriptionText.Text = Branding.Description;
        CopyrightText.Text = Branding.Copyright;
        WebsiteButton.Content = Branding.WebsiteUrl;
        VersionText.Text = $"Version {GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";
    }

    private void OnWebsiteClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = Branding.WebsiteUrl,
            UseShellExecute = true
        });
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
