using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace VStoVSC.Util;

/// <summary>
/// Avalonia の StorageProvider を用いたファイル選択サービス
/// </summary>
public sealed class FilePickerService : IFilePickerService
{
    private readonly Func<IStorageProvider> _getStorageProvider;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="getStorageProvider">StorageProvider を取得するデリゲート（例: Window から取得）</param>
    public FilePickerService(Func<IStorageProvider> getStorageProvider)
    {
        _getStorageProvider = getStorageProvider;
    }

    /// <inheritdoc />
    public async Task<string?> PickSolutionFileAsync()
    {
        var provider = _getStorageProvider();
        var options = new FilePickerOpenOptions
        {
            Title = App.Text("FilePicker.Title"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(App.Text("FilePicker.SolutionFiles"))
                {
                    Patterns = ["*.sln", "*.slnx"]
                },
                new FilePickerFileType(App.Text("FilePicker.AllFiles")) { Patterns = ["*.*"] }
            ]
        };

        var files = await provider.OpenFilePickerAsync(options);
        var file = files.Count > 0 ? files[0] : null;
        if (file == null) return null; // ダイアログのキャンセル
        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
            throw new System.IO.IOException(App.Text("Result.Error.LocalPathUnavailable"));
        return path;
    }
}
