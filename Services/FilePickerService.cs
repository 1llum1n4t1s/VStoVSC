using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace VS_to_VSC.Services;

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
            Title = "Visual Studioソリューションファイルを選択してください",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("ソリューションファイル")
                {
                    Patterns = ["*.sln", "*.slnx"]
                },
                new FilePickerFileType("すべてのファイル") { Patterns = ["*.*"] }
            ]
        };

        var files = await provider.OpenFilePickerAsync(options);
        var file = files.Count > 0 ? files[0] : null;
        return file?.TryGetLocalPath();
    }
}
