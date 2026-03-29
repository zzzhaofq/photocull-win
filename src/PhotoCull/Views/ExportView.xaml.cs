using System.Windows;
using System.Windows.Controls;
using PhotoCull.ViewModels;
using Microsoft.Win32;

namespace PhotoCull.Views;

public partial class ExportView : UserControl
{
    private readonly MainViewModel _vm;

    public ExportView(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateStats();
    }

    private void UpdateStats()
    {
        SelectedCountText.Text = _vm.ExportVm.SelectedPhotos.Count.ToString();
        RejectedCountText.Text = _vm.ExportVm.RejectedPhotos.Count.ToString();
        TotalCountText.Text = _vm.ExportVm.TotalPhotos.ToString();
    }

    private void OnSelectTargetFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择导出目标文件夹" };
        if (dialog.ShowDialog() == true)
        {
            _vm.ExportVm.TargetFolderPath = dialog.FolderName;
            TargetFolderText.Text = dialog.FolderName;
        }
    }

    private void OnMinRatingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MinRatingCombo?.SelectedIndex >= 0)
            _vm.ExportVm.MinExportRating = MinRatingCombo.SelectedIndex;
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_vm.ExportVm.TargetFolderPath))
        {
            MessageBox.Show("请先选择目标文件夹", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await _vm.ExportVm.ExportAsync();
    }
}
