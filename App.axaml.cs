using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Velopack;

namespace VS_to_VSC;

/// <summary>
/// App.axamlのロジックを管理するクラス
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
        VelopackApp.Build().Run();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.Startup += (_, _) => _ = _updateService.TryUpdateAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
