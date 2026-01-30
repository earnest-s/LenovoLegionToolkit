using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF.Pages;

public partial class AboutPage
{
    private static string VersionText
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            if (version is null)
                return string.Empty;
            return version.ToString(3);
        }
    }

    private static string BuildText
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            if (version is null)
                return string.Empty;
            return version.Revision.ToString();
        }
    }

    private static string CopyrightText => "© 2026 Earnest S";

    public AboutPage()
    {
        InitializeComponent();

        _version.Text += $" {VersionText}";
        _build.Text += $" {BuildText}";
        _copyright.Text = CopyrightText;

        _translationCredit.Visibility = Resource.Culture.Equals(new CultureInfo("en")) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OpenApplicationDataFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(Folders.AppData))
            return;

        Process.Start("explorer", Folders.AppData);
    }

    private void OpenApplicationTempFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(Folders.Temp))
            return;

        Process.Start("explorer", Folders.Temp);
    }
}
