using System;
using System.Runtime.InteropServices;
using Avalonia;
using Velopack;
using VStoVSC.Util;

namespace VStoVSC;

/// <summary>
/// アプリケーションのエントリポイント
/// </summary>
internal static partial class Program
{
    private const string AppUserModelId = "velopack.VStoVSC";

    [STAThread]
    public static void Main(string[] args)
    {
        if (OperatingSystem.IsWindows())
        {
            TrySetCurrentProcessAppUserModelId();
        }

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

    private static void TrySetCurrentProcessAppUserModelId()
    {
        try { _ = SetCurrentProcessExplicitAppUserModelID(AppUserModelId); }
        catch { /* シェル連携の失敗だけで起動を止めない */ }
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SetCurrentProcessExplicitAppUserModelID(string appId);
}
