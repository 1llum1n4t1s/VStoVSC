namespace VS_to_VSC.Services;

/// <summary>
/// ファイル選択ダイアログを提供するサービスのインターフェース
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// ソリューションファイルを選択するダイアログを表示する
    /// </summary>
    /// <returns>選択されたファイルのパス。キャンセル時はnull</returns>
    Task<string?> PickSolutionFileAsync();
}
