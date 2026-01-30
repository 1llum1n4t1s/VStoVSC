using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VS_to_VSC.Services;
using VS_to_VSC.ViewModels;

namespace VS_to_VSC.Views;

/// <summary>
/// メインウィンドウの View
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// コンストラクタ
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        var filePicker = new FilePickerService(() => StorageProvider);
        var dialog = new DialogService(() => this);
        var generator = new VSCodeGenerator(_ => { });
        DataContext = new MainWindowViewModel(filePicker, dialog, generator);
        if (this.FindControl<Border>("DropArea") is { } dropArea)
        {
            dropArea.AddHandler(DragDrop.DragOverEvent, DropArea_DragOver);
            dropArea.AddHandler(DragDrop.DropEvent, DropArea_Drop);
            dropArea.AddHandler(DragDrop.DragLeaveEvent, DropArea_DragLeave);
        }
    }

    private async void DropArea_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && DataContext is MainWindowViewModel vm)
            await vm.PickFileAndConvertAsync().ConfigureAwait(true);
    }

    private void DropArea_DragOver(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
            vm.IsDragOver = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void DropArea_DragLeave(object? sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.IsDragOver = false;
    }

    private async void DropArea_Drop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;
        vm.IsDragOver = false;
        if (!e.DataTransfer.Contains(DataFormat.File))
            return;
        var files = e.DataTransfer.TryGetFiles();
        if (files is null || files.Length == 0)
            return;
        foreach (var item in files)
        {
            var path = item.TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
                continue;
            await vm.DropSolutionAsync(path).ConfigureAwait(true);
            break;
        }
    }
}
