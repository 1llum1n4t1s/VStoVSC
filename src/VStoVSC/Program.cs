using System;
using Avalonia;
using Velopack;
using VStoVSC.Util;

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
        var velopackApp = VelopackApp.Build();
        if (OperatingSystem.IsWindows())
        {
            velopackApp
                .OnAfterInstallFastCallback(_ => WindowsLegacyStartMenuShortcutMigrator.MigrateForCurrentUser())
                .OnAfterUpdateFastCallback(_ => WindowsLegacyStartMenuShortcutMigrator.MigrateForCurrentUser());
        }

        velopackApp.Run();

        // 更新フックで一時的なファイルロックが発生した場合も、通常起動時に再試行する。
        if (OperatingSystem.IsWindows())
        {
            WindowsLegacyStartMenuShortcutMigrator.MigrateForCurrentUser();
        }

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
