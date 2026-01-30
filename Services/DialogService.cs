using Avalonia.Controls;
using Avalonia.Interactivity;
using VS_to_VSC.Views.Dialogs;

namespace VS_to_VSC.Services;

/// <summary>
/// ウィンドウベースのメッセージ・確認ダイアログを提供するサービス
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly Func<Window?> _getOwner;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="getOwner">親ウィンドウを取得するデリゲート</param>
    public DialogService(Func<Window?> getOwner)
    {
        _getOwner = getOwner;
    }

    /// <inheritdoc />
    public async Task ShowInfoAsync(string title, string message)
    {
        await ShowMessageAsync(title, message, DialogKind.Info).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public async Task ShowWarningAsync(string title, string message)
    {
        await ShowMessageAsync(title, message, DialogKind.Warning).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public async Task ShowErrorAsync(string title, string message)
    {
        await ShowMessageAsync(title, message, DialogKind.Error).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmYesNoAsync(string title, string message)
    {
        var owner = _getOwner();
        var dialog = new ConfirmDialogWindow { Title = title, Message = message };
        var result = await dialog.ShowDialog<bool>(owner ?? throw new InvalidOperationException("親ウィンドウが取得できません。")).ConfigureAwait(true);
        return result;
    }

    private async Task ShowMessageAsync(string title, string message, DialogKind kind)
    {
        var owner = _getOwner();
        var dialog = new MessageDialogWindow { Title = title, Message = message, Kind = kind };
        await dialog.ShowDialog(owner ?? throw new InvalidOperationException("親ウィンドウが取得できません。")).ConfigureAwait(true);
    }

    private enum DialogKind { Info, Warning, Error }
}
