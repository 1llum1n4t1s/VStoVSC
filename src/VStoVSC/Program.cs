using System;
using Avalonia;
using Velopack;

namespace VStoVSC;

/// <summary>
/// アプリケーションのエントリポイント
/// </summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack のブートストラップを最初に走らせる
        // (--veloapp-install / --veloapp-updated 等の internal hook を捌くため、Avalonia 起動前に必須)
        VelopackApp.Build().Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Avaloniaアプリケーションをビルドする
    /// </summary>
    /// <returns>AppBuilderのインスタンス</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
