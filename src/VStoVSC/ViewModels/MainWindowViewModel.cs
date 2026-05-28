using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Reflection;
using VStoVSC.Util;

namespace VStoVSC.ViewModels;

/// <summary>
/// メインウィンドウの ViewModel (CommunityToolkit.Mvvm)
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IFilePickerService _filePicker;
    private readonly VSCodeGenerator _generator;
    private bool _disposed;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private bool _isCheckingUpdate;

    /// <summary>表示用バージョン文字列 (例: "v2.0.8")</summary>
    public string VersionText
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null) return "v0.0.0";
            return $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    /// <summary>著作権表示</summary>
    public string CopyrightText => App.Text("Version.Copyright");

    public MainWindowViewModel(IFilePickerService filePicker, VSCodeGenerator generator)
    {
        _filePicker = filePicker;
        _generator = generator;

        App.UpdateCheckStateChanged += OnUpdateCheckStateChanged;
    }

    private void OnUpdateCheckStateChanged(bool inProgress)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => IsCheckingUpdate = inProgress);
    }

    /// <summary>
    /// CodeRabbit #3312176240 対応: App.UpdateCheckStateChanged は static event なので、
    /// VM 破棄時に必ず解除しないとウィンドウ再生成でハンドラ重複 + メモリ保持 (leak) になる。
    /// MainWindow.OnClosed から (DataContext as IDisposable)?.Dispose() で呼ばれる。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        App.UpdateCheckStateChanged -= OnUpdateCheckStateChanged;
    }

    /// <summary>
    /// ファイル選択ダイアログを表示し、選択されたソリューションを変換する
    /// </summary>
    [RelayCommand]
    public async Task PickFileAndConvertAsync()
    {
        try
        {
            var path = await _filePicker.PickSolutionFileAsync();
            if (!string.IsNullOrEmpty(path))
                await StartConversionAsync(path);
        }
        catch (Exception ex)
        {
            Logger.LogException("ファイル選択中にエラー", ex);
            await MessageService.ShowException(App.Text("Result.Error.Title"), ex);
        }
    }

    /// <summary>
    /// 手動アップデートチェック
    /// </summary>
    [RelayCommand]
    public void CheckForUpdate()
    {
        App.Check4Update(manually: true);
    }

    /// <summary>
    /// ドロップされたファイルパスで変換を開始する (View から呼ばれる)
    /// </summary>
    public async Task DropSolutionAsync(string solutionPath)
    {
        await StartConversionAsync(solutionPath);
    }

    /// <summary>
    /// 変換処理を実行する
    /// </summary>
    private async Task StartConversionAsync(string solutionPath)
    {
        try
        {
            if (!await ValidateSolutionPathAsync(solutionPath))
                return;

            await _generator.GenerateVSCodeFilesAsync(solutionPath,
                message => MessageService.ShowYesNoQuestionAsync(message, App.Text("Dialog.Confirm")));

            var successMessage = App.Text("Result.Success.Message", Path.GetFileName(solutionPath));
            await MessageService.ShowSuccess(successMessage, App.Text("Result.Success.Title"));
        }
        catch (Exception ex)
        {
            Logger.LogException("変換処理中にエラー", ex);
            await MessageService.ShowException(App.Text("Result.Error.Generic", ex.Message), ex,
                App.Text("Result.Error.Title"));
        }
    }

    /// <summary>
    /// ソリューションパスを検証する
    /// </summary>
    private static async Task<bool> ValidateSolutionPathAsync(string solutionPath)
    {
        if (string.IsNullOrEmpty(solutionPath))
        {
            await MessageService.ShowWarning(App.Text("Result.Error.FileNotFound", "(empty)"),
                App.Text("Result.Error.Title"));
            return false;
        }

        if (!File.Exists(solutionPath))
        {
            await MessageService.ShowError(App.Text("Result.Error.FileNotFound", solutionPath),
                App.Text("Result.Error.Title"));
            return false;
        }

        var extension = Path.GetExtension(solutionPath).ToLowerInvariant();
        if (extension is not ".sln" and not ".slnx")
        {
            await MessageService.ShowError(App.Text("Result.Error.InvalidExtension"),
                App.Text("Result.Error.Title"));
            return false;
        }

        return true;
    }
}
