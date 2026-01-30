using System.Windows.Input;

namespace VS_to_VSC.ViewModels;

/// <summary>
/// デリゲートで実行内容を指定する ICommand 実装
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="execute">実行する処理</param>
    /// <param name="canExecute">実行可能判定（省略時は常に true）</param>
    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter) => _execute(parameter);

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// CanExecute の変更を通知する
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
