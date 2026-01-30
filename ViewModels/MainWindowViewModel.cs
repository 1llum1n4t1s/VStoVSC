using System.IO;
using System.Windows.Input;
using VS_to_VSC.Services;

namespace VS_to_VSC.ViewModels;

/// <summary>
/// メインウィンドウの ViewModel
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private bool _isDragOver;
    private readonly IFilePickerService _filePicker;
    private readonly IDialogService _dialog;
    private readonly VSCodeGenerator _generator;

    /// <summary>
    /// ドロップエリア上にドラッグ中かどうか
    /// </summary>
    public bool IsDragOver
    {
        get => _isDragOver;
        set => SetProperty(ref _isDragOver, value);
    }

    /// <summary>
    /// ファイル選択（クリック）コマンド
    /// </summary>
    public ICommand PickFileCommand { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="filePicker">ファイル選択サービス</param>
    /// <param name="dialog">ダイアログサービス</param>
    /// <param name="generator">VSCode 設定生成サービス</param>
    public MainWindowViewModel(IFilePickerService filePicker, IDialogService dialog, VSCodeGenerator generator)
    {
        _filePicker = filePicker;
        _dialog = dialog;
        _generator = generator;
        PickFileCommand = new RelayCommand(_ => _ = PickFileAndConvertAsync());
    }

    /// <summary>
    /// ファイル選択ダイアログを表示し、選択されたソリューションを変換する
    /// </summary>
    public async Task PickFileAndConvertAsync()
    {
        try
        {
            var path = await _filePicker.PickSolutionFileAsync().ConfigureAwait(true);
            if (!string.IsNullOrEmpty(path))
                await StartConversionAsync(path).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _dialog.ShowErrorAsync("ファイル選択エラー",
                $"ファイル選択中にエラーが発生しました。\n\nエラー内容: {ex.Message}").ConfigureAwait(true);
        }
    }

    /// <summary>
    /// ドロップされたファイルパスで変換を開始する（View から呼ばれる）
    /// </summary>
    /// <param name="solutionPath">ソリューションファイルのパス</param>
    public async Task DropSolutionAsync(string solutionPath)
    {
        await StartConversionAsync(solutionPath).ConfigureAwait(true);
    }

    /// <summary>
    /// 変換処理を実行する
    /// </summary>
    /// <param name="solutionPath">ソリューションファイルのパス</param>
    private async Task StartConversionAsync(string solutionPath)
    {
        try
        {
            if (!await ValidateSolutionPathAsync(solutionPath).ConfigureAwait(true))
                return;

            await _generator.GenerateVSCodeFilesAsync(solutionPath,
                message => _dialog.ConfirmYesNoAsync("確認", message)).ConfigureAwait(true);

            var successMessage = $"変換が完了しました。\n\nソリューションファイル: {Path.GetFileName(solutionPath)}\n\n.vscodeフォルダに設定ファイルが生成されました。";
            await _dialog.ShowInfoAsync("変換完了", successMessage).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _dialog.ShowErrorAsync("変換エラー",
                $"変換処理中にエラーが発生しました。\n\nエラー内容: {ex.Message}").ConfigureAwait(true);
        }
    }

    /// <summary>
    /// ソリューションパスを検証する
    /// </summary>
    /// <param name="solutionPath">検証するパス</param>
    /// <returns>有効な場合 true</returns>
    private async Task<bool> ValidateSolutionPathAsync(string solutionPath)
    {
        if (string.IsNullOrEmpty(solutionPath))
        {
            await _dialog.ShowWarningAsync("入力エラー",
                "ファイルパスが入力されていません。\n\nソリューションファイルのパスを入力してください。").ConfigureAwait(true);
            return false;
        }

        if (!File.Exists(solutionPath))
        {
            await _dialog.ShowErrorAsync("ファイル不存在エラー",
                $"指定されたファイルが存在しません。\n\nパス: {solutionPath}\n\n正しいファイルパスを確認してください。").ConfigureAwait(true);
            return false;
        }

        var extension = Path.GetExtension(solutionPath).ToLowerInvariant();
        if (extension is not ".sln" and not ".slnx")
        {
            await _dialog.ShowErrorAsync("ファイル形式エラー",
                $"サポートされていないファイル形式です。\n\nファイル: {Path.GetFileName(solutionPath)}\n拡張子: {extension}\n\n.slnまたは.slnxファイルを選択してください。").ConfigureAwait(true);
            return false;
        }

        return true;
    }
}
