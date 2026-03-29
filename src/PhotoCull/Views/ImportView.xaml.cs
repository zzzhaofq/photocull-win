using System.IO;
using System.Windows;
using System.Windows.Controls;
using PhotoCull.Data;
using PhotoCull.ViewModels;
using Microsoft.Win32;

namespace PhotoCull.Views;

public partial class ImportView : UserControl
{
    private readonly MainViewModel _vm;

    public ImportView(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var sessions = await _vm.GetRecentSessionsAsync();
            if (sessions.Count > 0)
            {
                RecentLabel.Visibility = Visibility.Visible;
                RecentSessionsList.Visibility = Visibility.Visible;
                RecentSessionsList.ItemsSource = sessions;
            }
        }
        catch { }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            var path = files[0];
            if (Directory.Exists(path))
                await StartImport(path);
        }
    }

    private async void OnSelectFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择照片文件夹" };
        if (dialog.ShowDialog() == true)
        {
            await StartImport(dialog.FolderName);
        }
    }

    private async Task StartImport(string folderPath)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        using var db = new PhotoCullDbContext();
        await _vm.ImportVm.ImportFolderAsync(folderPath, db);

        if (_vm.ImportVm.ErrorMessage != null)
        {
            ErrorText.Text = _vm.ImportVm.ErrorMessage;
            ErrorText.Visibility = Visibility.Visible;
        }
        else if (_vm.ImportVm.CompletedSession != null)
        {
            _vm.OnImportComplete(_vm.ImportVm.CompletedSession);
        }
    }

    private void OnCancelImport(object sender, RoutedEventArgs e)
    {
        _vm.ImportVm.CancelImport();
    }

    private void OnResumeSession(object sender, SelectionChangedEventArgs e)
    {
        if (RecentSessionsList.SelectedItem is Models.CullingSession session)
        {
            _vm.ResumeSession(session);
            RecentSessionsList.SelectedItem = null;
        }
    }
}
