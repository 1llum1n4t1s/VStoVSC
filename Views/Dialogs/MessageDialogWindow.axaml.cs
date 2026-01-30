using Avalonia.Controls;
using Avalonia.Interactivity;

namespace VS_to_VSC.Views.Dialogs;

/// <summary>
/// メッセージ表示用のモーダルダイアログ
/// </summary>
public partial class MessageDialogWindow : Window
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
    /// ダイアログの種類（Info / Warning / Error）
    /// </summary>
    public object? Kind { get; set; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public MessageDialogWindow()
    {
        InitializeComponent();
        Opened += (_, _) => MessageText!.Text = _message;
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
