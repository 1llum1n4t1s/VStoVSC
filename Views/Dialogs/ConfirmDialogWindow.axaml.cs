using Avalonia.Controls;
using Avalonia.Interactivity;

namespace VS_to_VSC.Views.Dialogs;

/// <summary>
/// Yes/No 確認用のモーダルダイアログ
/// </summary>
public partial class ConfirmDialogWindow : Window
{
    private string _message = string.Empty;

    /// <summary>
    /// 表示するメッセージ
    /// </summary>
    public string Message
    {
        get => _message;
        set
        {
            _message = value ?? string.Empty;
            if (MessageText != null)
                MessageText.Text = _message;
        }
    }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public ConfirmDialogWindow()
    {
        InitializeComponent();
        Opened += (_, _) => MessageText!.Text = _message;
    }

    private void YesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void NoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
