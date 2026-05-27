using System.Diagnostics;

namespace VStoVSC.Util;

/// <summary>
/// アプリケーション実行ファイルのパス解決を一元管理する
/// </summary>
public static class AppPathResolver
{
    private static readonly Lazy<string> _executablePath = new(Resolve);

    /// <summary>アプリケーション実行ファイルのパス（キャッシュ済み）</summary>
    public static string ExecutablePath => _executablePath.Value;

    private static string Resolve()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                return processPath;

            using var process = Process.GetCurrentProcess();
            processPath = process.MainModule?.FileName;
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                return processPath;

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var exeFiles = Directory.GetFiles(baseDirectory, "*.exe");
            if (exeFiles.Length > 0)
            {
                var mainExe = exeFiles.FirstOrDefault(f =>
                    Path.GetFileName(f).Equals("VStoVSC.exe", StringComparison.OrdinalIgnoreCase));
                return mainExe ?? exeFiles[0];
            }

            var assemblyPath = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                var exePath = Path.Combine(assemblyPath.TrimEnd(Path.DirectorySeparatorChar), "VStoVSC.exe");
                if (File.Exists(exePath))
                    return exePath;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Logger.LogException("実行ファイルパスの取得に失敗しました", ex);
            return string.Empty;
        }
    }
}
