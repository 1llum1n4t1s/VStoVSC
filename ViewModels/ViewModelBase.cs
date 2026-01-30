using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VS_to_VSC.ViewModels;

/// <summary>
/// INotifyPropertyChanged を実装した ViewModel の基底クラス
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// プロパティ変更を通知する
    /// </summary>
    /// <param name="propertyName">変更したプロパティ名（省略時は呼び出し元のメンバ名）</param>
    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// プロパティを設定し、変更時に通知する
    /// </summary>
    /// <typeparam name="T">プロパティの型</typeparam>
    /// <param name="field">バッキングフィールドへの参照</param>
    /// <param name="value">設定する値</param>
    /// <param name="propertyName">プロパティ名（省略時は呼び出し元のメンバ名）</param>
    /// <returns>値が変更された場合 true</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }
}
