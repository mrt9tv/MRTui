using System.Windows.Controls;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF.Pages;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        TxtVersion.Text = $"v{VersionInfo.DisplayVersion}";
    }
}
