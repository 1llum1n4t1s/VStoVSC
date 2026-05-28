using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VStoVSC.Util;

/// <summary>
/// アプリケーション設定
/// </summary>
public sealed class Settings
{
    /// <summary>
    /// アプリケーションデータディレクトリ
    /// </summary>
    internal static readonly string AppDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VStoVSC");

    /// <summary>
    /// 設定ファイルのパス
    /// </summary>
    private static readonly string SettingsFilePath = Path.Combine(AppDataDirectory, "settings.json");

    /// <summary>
    /// 自動更新で許可する R2 配信元の正規 URL（悪意ある誘導を防ぐためハードコード固定）。
    /// Velopack の <see cref="Velopack.Sources.SimpleWebSource"/> がこの base URL + <c>/releases.{channel}.json</c> を取得する。
    /// </summary>
    internal const string CanonicalUpdateBaseUrl = "https://vs2vsc.nephilim.jp";

    /// <summary>テーマ ("System" / "Light" / "Dark")</summary>
    public string Theme { get; set; } = "System";

    /// <summary>ロケール ("ja_JP" / "en_US" 等、空文字列はシステム自動検出)</summary>
    public string Locale { get; set; } = "";

    /// <summary>自動更新 base URL (ハードコード固定)</summary>
    [JsonIgnore]
    public string UpdateBaseUrl => CanonicalUpdateBaseUrl;

    /// <summary>自動更新チャンネル</summary>
    public string UpdateChannel { get; set; } = "win";

    /// <summary>メイン画面起動時に自動更新チェックを走らせるか</summary>
    public bool Check4UpdatesOnStartup { get; set; } = true;

    /// <summary>スキップする Velopack リリースタグ名（空文字列は未設定）</summary>
    public string IgnoreUpdateTag { get; set; } = "";

    /// <summary>ログファイルの最大サイズ (MB)</summary>
    public int LogMaxSizeMB { get; set; } = 10;

    /// <summary>ログファイルの保持日数 (0以下なら削除しない)</summary>
    public int LogRetentionDays { get; set; } = 7;

    /// <summary>サポートされているテーマ一覧</summary>
    public static readonly string[] SupportedThemes = ["System", "Dark", "Light"];

    /// <summary>サポートされている自動更新チャンネル</summary>
    public static readonly string[] SupportedUpdateChannels = ["win"];

    /// <summary>
    /// 並列アクセスに対して安全なスナップショット（浅いコピー）を返す
    /// </summary>
    public Settings Snapshot() => (Settings)MemberwiseClone();

    /// <summary>
    /// 設定をファイルから読み込む
    /// </summary>
    public static Settings Load()
    {
        Settings? settings = null;
        try
        {
            if (!Directory.Exists(AppDataDirectory))
                Directory.CreateDirectory(AppDataDirectory);

            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                try
                {
                    settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.Settings);
                }
                catch (JsonException ex)
                {
                    var backupPath = $"{SettingsFilePath}.corrupt_{DateTime.Now:yyyyMMddHHmmss_fff}.bak";
                    try { File.Move(SettingsFilePath, backupPath, overwrite: true); }
                    catch { try { File.Delete(SettingsFilePath); } catch { } }
                    Debug.WriteLine($"設定ファイルの解析に失敗しました（デフォルトに戻します）: {ex.Message}");
                }

                settings?.SanitizeAfterLoad();
            }
            else
            {
                var defaultSettings = new Settings();
                defaultSettings.Save();
                settings = defaultSettings;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"設定ファイルの読み込みに失敗しました: {ex.Message}");
        }

        return settings ?? new Settings();
    }

    /// <summary>
    /// Load 直後に不正値をデフォルトに戻すサニタイズ処理
    /// </summary>
    internal void SanitizeAfterLoad()
    {
        UpdateChannel = Array.Find(SupportedUpdateChannels,
                            c => string.Equals(c, UpdateChannel, StringComparison.OrdinalIgnoreCase))
                        ?? "win";

        Theme = Array.Find(SupportedThemes, t => string.Equals(t, Theme, StringComparison.OrdinalIgnoreCase))
                ?? "System";

        IgnoreUpdateTag ??= "";
        if (IgnoreUpdateTag.Length > 256 || IgnoreUpdateTag.AsSpan().IndexOfAnyInRange('\0', '\x1F') >= 0)
            IgnoreUpdateTag = "";
        else
            IgnoreUpdateTag = IgnoreUpdateTag.Trim();

        LogMaxSizeMB = Math.Clamp(LogMaxSizeMB, 1, 200);
        LogRetentionDays = Math.Clamp(LogRetentionDays, 0, 365);
    }

    /// <summary>
    /// 設定をファイルに保存する (atomic 書込)
    /// </summary>
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, AppJsonContext.Default.Settings);
            WriteAtomically(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"設定の保存に失敗しました: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// tmp + Move overwrite で atomic に書き込む
    /// </summary>
    private static void WriteAtomically(string destinationPath, string content)
    {
        var tmpPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tmpPath, content);
            File.Move(tmpPath, destinationPath, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
            throw;
        }
    }
}
