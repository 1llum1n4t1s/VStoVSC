using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using VStoVSC.Util;
using VStoVSC.ViewModels;

namespace VStoVSC.View;

/// <summary>
/// メインウィンドウ (View 純粋、ロジックは ViewModel に委譲)
/// </summary>
public partial class MainWindow : Window
{
    private Border? _dropOverlay;
    private Border? _accentOverlay;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            _dropOverlay = this.FindControl<Border>("DropOverlay");
            _accentOverlay = this.FindControl<Border>("AccentOverlay");
            ApplyAccentOverlay();

            var filePicker = new FilePickerService(() => StorageProvider);
            var generator = new VSCodeGenerator(_ => { });
            DataContext = new MainWindowViewModel(filePicker, generator);
        }
        catch (Exception ex)
        {
            Logger.LogException("MainWindow 初期化エラー", ex);
            _ = MessageService.ShowException(App.Text("Result.Error.Title"), ex);
            throw;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// OS のアクセントカラーを取得してオーバーレイに適用
    /// </summary>
    private void ApplyAccentOverlay()
    {
        if (_accentOverlay is null) return;
        try
        {
            var colors = Application.Current?.PlatformSettings?.GetColorValues();
            if (colors is { } c)
                _accentOverlay.Background = new SolidColorBrush(c.AccentColor1);
        }
        catch
        {
            // アクセントカラー取得失敗時はオーバーレイなし
        }
    }

    /// <summary>
    /// D&D エリアをクリック → ファイル選択ダイアログ
    /// </summary>
    private async void DropArea_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && DataContext is MainWindowViewModel vm)
            await vm.PickFileAndConvertAsync();
    }

    /// <summary>
    /// ドラッグオーバー：オーバーレイを表示 + DragEffects.Copy 設定
    /// </summary>
    private void DropZone_DragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
            if (_dropOverlay != null)
                _dropOverlay.IsVisible = true;
            if (DataContext is MainWindowViewModel vm)
                vm.IsDragOver = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    /// <summary>
    /// ドラッグ離脱：オーバーレイを非表示
    /// </summary>
    private void DropZone_DragLeave(object? sender, DragEventArgs e)
    {
        if (_dropOverlay != null)
            _dropOverlay.IsVisible = false;
        if (DataContext is MainWindowViewModel vm)
            vm.IsDragOver = false;
    }

    /// <summary>
    /// ドロップ：ファイルパスを抽出して VM に渡す
    /// </summary>
    private async void DropZone_Drop(object? sender, DragEventArgs e)
    {
        if (_dropOverlay != null)
            _dropOverlay.IsVisible = false;
        if (DataContext is not MainWindowViewModel vm) return;
        vm.IsDragOver = false;

        if (!e.DataTransfer.Contains(DataFormat.File)) return;

        var files = e.DataTransfer.TryGetFiles();
        if (files is null || files.Length == 0) return;

        foreach (var item in files)
        {
            var path = item.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) continue;
            await vm.DropSolutionAsync(path);
            break;
        }
    }
}
