namespace VS_to_VSC.Services;

/// <summary>
/// メッセージダイアログを提供するサービスのインターフェース
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// 情報メッセージを表示する
    /// </summary>
    /// <param name="title">タイトル</param>
    /// <param name="message">メッセージ本文</param>
    Task ShowInfoAsync(string title, string message);

    /// <summary>
    /// 警告メッセージを表示する
    /// </summary>
    /// <param name="title">タイトル</param>
    /// <param name="message">メッセージ本文</param>
    Task ShowWarningAsync(string title, string message);

    /// <summary>
    /// エラーメッセージを表示する
    /// </summary>
    /// <param name="title">タイトル</param>
    /// <param name="message">メッセージ本文</param>
    Task ShowErrorAsync(string title, string message);

    /// <summary>
    /// Yes/No 確認ダイアログを表示する
    /// </summary>
    /// <param name="title">タイトル</param>
    /// <param name="message">メッセージ本文</param>
    /// <returns>Yes が選択された場合 true、No またはキャンセル時 false</returns>
    Task<bool> ConfirmYesNoAsync(string title, string message);
}
