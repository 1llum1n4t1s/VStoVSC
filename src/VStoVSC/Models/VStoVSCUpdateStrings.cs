using System.ComponentModel;
using VelopackUpdateDialog;

namespace VStoVSC.Models;

/// <summary>
/// VelopackUpdateDialog.Avalonia が要求する文字列セット (<see cref="IUpdateDialogStrings"/>) を、
/// VStoVSC の Locale ResourceDictionary (Text.SelfUpdate.* / Text.Close) 経由で動的に解決する。
/// </summary>
public sealed class VStoVSCUpdateStrings : IUpdateDialogStrings, INotifyPropertyChanged
{
    /// <summary>SelfUpdate 系 Locale キーの共通プレフィクス。</summary>
    private const string KeyPrefix = "SelfUpdate.";

    /// <summary>シングルトン インスタンス。</summary>
    public static VStoVSCUpdateStrings Instance { get; } = new();

    private VStoVSCUpdateStrings() { }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>言語切替時に呼ぶと全プロパティの再評価を XAML バインディングに通知する</summary>
    public void NotifyLocaleChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    /// <inheritdoc />
    public string Title => App.Text(KeyPrefix + "Title");

    /// <inheritdoc />
    public string AvailableHeader => App.Text(KeyPrefix + "Available");

    /// <inheritdoc />
    public string DownloadAndInstall => App.Text(KeyPrefix + "DownloadAndInstall");

    /// <inheritdoc />
    public string IgnoreThisVersion => App.Text(KeyPrefix + "IgnoreThisVersion");

    /// <inheritdoc />
    public string UpToDateMessage => App.Text(KeyPrefix + "UpToDate");

    /// <inheritdoc />
    public string ErrorHeader => App.Text(KeyPrefix + "Error");

    /// <inheritdoc />
    public string Close => App.Text("Close");

    /// <inheritdoc />
    public string CheckingMessage => App.Text(KeyPrefix + "Checking");
}
