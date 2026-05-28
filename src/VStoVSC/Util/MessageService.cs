using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace VStoVSC.Util;

/// <summary>
/// メッセージボックス表示を一元管理するサービスクラス (MessageBox.Avalonia 使用)
/// </summary>
public static class MessageService
{
    internal static async Task<Window?> GetActiveWindowAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        if (Dispatcher.UIThread.CheckAccess())
            return GetActiveWindowInternal(desktop);

        return await Dispatcher.UIThread.InvokeAsync(() => GetActiveWindowInternal(desktop));
    }

    private static Window? GetActiveWindowInternal(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var activeWindow = desktop.Windows.FirstOrDefault(w => w.IsActive && w.IsVisible);
        if (activeWindow != null) return activeWindow;
        var lastVisibleWindow = desktop.Windows.LastOrDefault(w => w.IsVisible);
        if (lastVisibleWindow != null) return lastVisibleWindow;
        return desktop.MainWindow;
    }

    private static async Task ShowMessageAsync(string message, string title, Icon icon, LogLevel logLevel = LogLevel.Info)
    {
        Logger.Log($"{icon}メッセージ表示: {title} - {message}", logLevel);
        var window = await GetActiveWindowAsync();
        var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok, icon);
        if (window != null)
            await box.ShowWindowDialogAsync(window);
        else
            await box.ShowAsync();
    }

    public static Task ShowError(string message, string? title = null)
        => ShowMessageAsync(message, title ?? App.Text("Dialog.Error"), Icon.Error, LogLevel.Error);

    public static Task ShowInfo(string message, string? title = null)
        => ShowMessageAsync(message, title ?? App.Text("Dialog.Info"), Icon.Info);

    public static Task ShowWarning(string message, string? title = null)
        => ShowMessageAsync(message, title ?? App.Text("Dialog.Warning"), Icon.Warning, LogLevel.Warning);

    public static async Task ShowException(string context, Exception ex, string? title = null)
    {
        Logger.LogException(context, ex);
        var message = $"{context}\n\n{ex.Message}";
        title ??= App.Text("Dialog.Error");
        var window = await GetActiveWindowAsync();
        var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok, Icon.Error);
        if (window != null)
            await box.ShowWindowDialogAsync(window);
        else
            await box.ShowAsync();
    }

    public static Task ShowSuccess(string message, string? title = null)
        => ShowMessageAsync(message, title ?? App.Text("Dialog.Completed"), Icon.Success);

    /// <summary>
    /// はい/いいえの確認ダイアログを表示する
    /// </summary>
    public static async Task<bool> ShowYesNoQuestionAsync(string message, string title, Window? parentWindow = null)
    {
        parentWindow ??= await GetActiveWindowAsync();
        var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.YesNo, Icon.Question);
        ButtonResult result;
        if (parentWindow != null)
            result = await box.ShowWindowDialogAsync(parentWindow);
        else
            result = await box.ShowAsync();
        return result == ButtonResult.Yes;
    }
}
