using System.Windows;
using System.Windows.Controls;
using PhotoCull.ViewModels;

namespace PhotoCull.Views;

public partial class LicenseView : UserControl
{
    private readonly MainViewModel _vm;

    public LicenseView(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
    }

    private void OnActivateClick(object sender, RoutedEventArgs e)
    {
        var code = LicenseCodeBox.Text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            ShowError("请输入授权码");
            return;
        }

        if (_vm.TryActivateLicense(code))
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
        else
        {
            var error = _vm.GetLicenseError(code) ?? "无效的授权码";
            ShowError(error);
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
