using SuperLightLogger;

namespace VStoVSC.Util;

/// <summary>
/// ログレベル
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// ロガー初期化設定
/// </summary>
public sealed class LoggerConfig
{
    public required string LogDirectory { get; init; }
    public required string FilePrefix { get; init; }
    public int MaxSizeMB { get; init; } = 10;
    public int MaxArchiveFiles { get; init; } = 10;
    public int RetentionDays { get; init; } = 7;
}

/// <summary>
/// SuperLightLogger を使用した汎用ログ出力クラス
/// </summary>
public static class Logger
{
    private static ILog? _logger;
    private static bool _isConfigured;
    private static readonly object _initLock = new();
    private static string _appName = "App";

    /// <summary>
    /// 最小ログレベル
    /// </summary>
    private static readonly LogLevel MinLogLevel =
#if DEBUG
        LogLevel.Debug;
#else
        LogLevel.Info;
#endif

    /// <summary>
    /// ロガーを初期化する
    /// </summary>
    public static void Initialize(LoggerConfig config)
    {
        if (_isConfigured) return;

        lock (_initLock)
        {
            if (_isConfigured) return;

            _appName = config.FilePrefix;
            Directory.CreateDirectory(config.LogDirectory);

            LogManager.Configure(builder =>
            {
                builder.AddSuperLightFile(opt =>
                {
                    opt.FileName = Path.Combine(config.LogDirectory, $"{config.FilePrefix}_${{date:format=yyyyMMdd}}.log");
                    opt.Layout = "${longdate} [${level:uppercase=true}] ${message}${onexception:inner=${newline}${exception:format=tostring}}";
                    opt.ArchiveAboveSize = (long)config.MaxSizeMB * 1024 * 1024;
                    opt.ArchiveFileName = Path.Combine(config.LogDirectory, $"{config.FilePrefix}_${{date:format=yyyyMMdd}}_{{#}}.log");
                    opt.ArchiveNumbering = ArchiveNumbering.Sequence;
                    opt.MaxArchiveFiles = config.MaxArchiveFiles;
                    opt.Encoding = System.Text.Encoding.UTF8;
                    opt.MinLevelName = "Trace";
                });

                builder.SetMinimumLevel(ToLevelName(MinLogLevel));
            });

            _logger = LogManager.GetLogger(config.FilePrefix);
            _isConfigured = true;
        }

        Log("Logger initialized", LogLevel.Debug);

        // 古いログのクリーンアップを非同期で実行
        var logDirectory = config.LogDirectory;
        var filePrefix = config.FilePrefix;
        var retentionDays = config.RetentionDays;
        _ = Task.Run(() =>
        {
            try { CleanupOldLogFiles(logDirectory, filePrefix, retentionDays); }
            catch { /* ベストエフォート */ }
        });
    }

    private static void CleanupOldLogFiles(string logDirectory, string filePrefix, int retentionDays)
    {
        if (retentionDays <= 0) return;

        try
        {
            var cutoffDate = DateTime.Now.Date.AddDays(-retentionDays);
            var logFiles = Directory.EnumerateFiles(logDirectory, $"{filePrefix}_*.log");

            foreach (var file in logFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var parts = fileName.Split('_');
                    if (parts.Length >= 2 && parts[1].Length == 8 &&
                        DateTime.TryParseExact(parts[1], "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fileDate)
                        && fileDate < cutoffDate)
                    {
                        File.Delete(file);
                    }
                }
                catch { /* 個別ファイルの削除失敗は続行 */ }
            }
        }
        catch { /* 全体失敗も続行 */ }
    }

    /// <summary>ログを出力する</summary>
    public static void Log(string message, LogLevel level = LogLevel.Info)
    {
        if (level < MinLogLevel) return;
        WriteToLogger(message, level);
    }

    /// <summary>例外情報を含むログを出力する（常に Error レベル）</summary>
    public static void LogException(string message, Exception exception)
    {
        if (_logger != null)
        {
            _logger.Error(message, exception);
            return;
        }

        WriteEmergencyLog(message, exception);
    }

    /// <summary>_logger 未初期化時の緊急ログ書き込み</summary>
    internal static void WriteEmergencyLog(string message, Exception exception)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                _appName);
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, $"{_appName}_emergency.log");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [ERROR] {message}{Environment.NewLine}{exception}{Environment.NewLine}";
            File.AppendAllText(path, line);
        }
        catch { /* 最終フォールバック失敗時は諦める */ }
    }

    /// <summary>ロガーを終了する</summary>
    public static void Dispose()
    {
        LogManager.Shutdown();
        _isConfigured = false;
    }

    private static void WriteToLogger(string message, LogLevel level)
    {
        if (_logger == null) return;

        switch (level)
        {
            case LogLevel.Debug: _logger.Debug(message); break;
            case LogLevel.Info: _logger.Info(message); break;
            case LogLevel.Warning: _logger.Warn(message); break;
            case LogLevel.Error: _logger.Error(message); break;
        }
    }

    private static string ToLevelName(LogLevel level) => level switch
    {
        LogLevel.Debug => "Debug",
        LogLevel.Info => "Info",
        LogLevel.Warning => "Warn",
        LogLevel.Error => "Error",
        _ => "Info"
    };
}
