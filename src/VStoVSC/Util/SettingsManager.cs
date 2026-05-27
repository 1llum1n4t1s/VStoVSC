namespace VStoVSC.Util;

/// <summary>
/// Settings のシングルトンマネージャ
/// </summary>
public sealed class SettingsManager
{
    private static readonly Lazy<SettingsManager> _instance = new(() => new SettingsManager());
    private readonly Settings _settings;
    private readonly object _lock = new();

    /// <summary>シングルトンインスタンス</summary>
    public static SettingsManager Instance => _instance.Value;

    /// <summary>現在の設定 (読み取り専用)</summary>
    public Settings Current => _settings;

    /// <summary>並列処理向けの浅いコピー</summary>
    public Settings CreateSnapshot()
    {
        lock (_lock) return _settings.Snapshot();
    }

    /// <summary>設定を変更する</summary>
    public void Mutate(Action<Settings> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        lock (_lock) mutator(_settings);
    }

    /// <summary>設定の変更と保存を atomic に行う</summary>
    public void MutateAndSave(Action<Settings> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        lock (_lock)
        {
            mutator(_settings);
            _settings.Save();
        }
        Logger.Log("設定を変更し保存しました");
    }

    private SettingsManager()
    {
        try
        {
            _settings = Settings.Load();
            Logger.Initialize(new LoggerConfig
            {
                LogDirectory = Settings.AppDataDirectory,
                FilePrefix = "VStoVSC",
                MaxSizeMB = _settings.LogMaxSizeMB,
                RetentionDays = _settings.LogRetentionDays
            });
            Logger.Log("設定を読み込みました");
        }
        catch (Exception ex)
        {
            try
            {
                Logger.Initialize(new LoggerConfig
                {
                    LogDirectory = Settings.AppDataDirectory,
                    FilePrefix = "VStoVSC",
                });
            }
            catch { /* ignore */ }
            Logger.LogException("設定の読み込みに失敗しました。デフォルト設定を使用します", ex);
            _settings = new Settings();
        }
    }

    /// <summary>設定を保存する</summary>
    public void Save()
    {
        try
        {
            lock (_lock) _settings.Save();
            Logger.Log("設定を保存しました");
        }
        catch (Exception ex)
        {
            Logger.LogException("設定の保存に失敗しました", ex);
            throw;
        }
    }
}
