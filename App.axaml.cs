using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VS_to_VSC.Services;
using VS_to_VSC.Views;

namespace VS_to_VSC;

/// <summary>
/// アプリケーションのエントリクラス
/// </summary>
public partial class App : Application
{
    private readonly UpdateService _updateService = new();

    /// <summary>
    /// アプリケーションの初期化
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// アプリケーションのフレームワーク初期化完了時の処理
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.Startup += (_, _) =>
            {
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
                {
                    _ = _updateService.TryUpdateAsync();
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
