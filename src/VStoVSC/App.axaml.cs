using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using System.Globalization;
using Velopack;
using Velopack.Sources;
using VStoVSC.Util;
using VStoVSC.View;

namespace VStoVSC;

/// <summary>
/// アプリケーションのエントリクラス
/// </summary>
public partial class App : Application
{
    /// <summary>サポートされているロケール一覧</summary>
    public static readonly string[] SupportedLocales =
    [
        "en_US", "ja_JP", "zh_CN", "zh_TW", "de_DE", "fr_FR", "es_ES",
        "it_IT", "pt_BR", "ru_RU", "uk_UA", "id_ID", "fil_PH", "ta_IN", "ko_KR",
        "la_VA", "sa_IN"
    ];

    /// <summary>ロケール表示名 (ネイティブ言語名)</summary>
    public static readonly Dictionary<string, string> LocaleDisplayNames = new()
    {
        ["en_US"] = "English",
        ["ja_JP"] = "日本語",
        ["zh_CN"] = "简体中文",
        ["zh_TW"] = "繁體中文",
        ["de_DE"] = "Deutsch",
        ["fr_FR"] = "Français",
        ["es_ES"] = "Español",
        ["it_IT"] = "Italiano",
        ["pt_BR"] = "Português (Brasil)",
        ["ru_RU"] = "Русский",
        ["uk_UA"] = "Українська",
        ["id_ID"] = "Bahasa Indonesia",
        ["fil_PH"] = "Tagalog",
        ["ta_IN"] = "தமிழ்",
        ["ko_KR"] = "한국어",
        ["la_VA"] = "Latina",
        ["sa_IN"] = "संस्कृतम्"
    };

    private IResourceProvider? _activeLocale;
    private string? _activeLocaleKey;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public App()
    {
        InitializeComponent();

        // Settings 初期化 (Logger も内部で初期化される)
        var settings = SettingsManager.Instance.CreateSnapshot();

        // テーマ設定を適用
        RequestedThemeVariant = GetThemeVariant(settings.Theme);

        // ロケール設定を適用
        var locale = string.IsNullOrEmpty(settings.Locale) ? DetectDefaultLocale() : settings.Locale;
        ApplyLocale(locale);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                desktop.MainWindow = new MainWindow();
            }
            catch (Exception ex)
            {
                Logger.LogException("MainWindow の作成に失敗しました", ex);
                desktop.Shutdown();
                return;
            }

            // 起動時自動更新チェック
            if (SettingsManager.Instance.Current.Check4UpdatesOnStartup)
                Check4Update(manually: false);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// テーマ文字列から ThemeVariant を取得する
    /// </summary>
    private static ThemeVariant GetThemeVariant(string theme) => theme switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };

    /// <summary>
    /// テーマを切り替える
    /// </summary>
    public static void SetTheme(string theme)
    {
        if (Current is App app)
            app.RequestedThemeVariant = GetThemeVariant(theme);
    }

    /// <summary>
    /// ロケールを切り替える
    /// </summary>
    public static void SetLocale(string localeKey)
    {
        if (Current is App app)
            app.ApplyLocale(localeKey);
    }

    /// <summary>
    /// 選択ロケールを MergedDictionaries に挿入する
    /// </summary>
    private void ApplyLocale(string localeKey)
    {
        if (string.IsNullOrEmpty(localeKey)) return;
        if (string.Equals(_activeLocaleKey, localeKey, StringComparison.OrdinalIgnoreCase)) return;

        // CodeRabbit #3312176135 対応: ResourceDictionary のインデクサは KeyNotFoundException を投げるため
        // TryGetResource を使って未登録キーでも例外を出さずに警告ログだけで早期 return する。
        if (!Resources.TryGetResource(localeKey, null, out var resource) || resource is not IResourceProvider targetLocale)
        {
            Logger.Log($"未登録のロケールが指定されました: {localeKey}", LogLevel.Warning);
            return;
        }

        if (_activeLocale != null)
            Resources.MergedDictionaries.Remove(_activeLocale);

        Resources.MergedDictionaries.Add(targetLocale);
        _activeLocale = targetLocale;
        _activeLocaleKey = localeKey;

        // VelopackUpdateDialog に翻訳変更を通知
        Models.VStoVSCUpdateStrings.Instance.NotifyLocaleChanged();
    }

    /// <summary>
    /// システムのカルチャからデフォルトロケールを検出する
    /// </summary>
    public static string DetectDefaultLocale()
    {
        var culture = CultureInfo.CurrentUICulture;
        var name = culture.Name.Replace('-', '_');

        if (SupportedLocales.Contains(name))
            return name;

        var lang = culture.TwoLetterISOLanguageName;
        var match = SupportedLocales.FirstOrDefault(l => l.StartsWith(lang + "_", StringComparison.OrdinalIgnoreCase));
        return match ?? "en_US";
    }

    /// <summary>
    /// キーからフォーマット文字列を取得する
    /// </summary>
    private static string GetLocalizedFormat(string key)
    {
        var fullKey = string.Concat("Text.", key);
        string? fmt = null;

        if (Current is App app && app._activeLocale != null)
        {
            app._activeLocale.TryGetResource(fullKey, null, out var value);
            fmt = value as string;
        }

        if (string.IsNullOrWhiteSpace(fmt) && Current?.TryFindResource(fullKey, out var fallback) == true)
            fmt = fallback as string;

        if (string.IsNullOrWhiteSpace(fmt))
            return fullKey;

        if (fmt.Contains("\\n", StringComparison.Ordinal))
            fmt = fmt.Replace("\\n", "\n");

        return fmt;
    }

    /// <summary>引数なしのローカライズ済みテキスト取得</summary>
    public static string Text(string key) => GetLocalizedFormat(key);

    /// <summary>引数 1 つのローカライズ済みテキスト取得</summary>
    public static string Text(string key, object? arg0)
        => string.Format(GetLocalizedFormat(key), arg0);

    /// <summary>引数 2 つのローカライズ済みテキスト取得</summary>
    public static string Text(string key, object? arg0, object? arg1)
        => string.Format(GetLocalizedFormat(key), arg0, arg1);

    /// <summary>可変引数版</summary>
    public static string Text(string key, params object[] args)
    {
        var fmt = GetLocalizedFormat(key);
        return args.Length == 0 ? fmt : string.Format(fmt, args);
    }

    /// <summary>更新チェック中かどうかのアトミックフラグ</summary>
    private static int _isCheckingUpdate;

    /// <summary>更新チェック進行中かどうかの観測プロパティ</summary>
    public static bool IsUpdateCheckInProgress =>
        Interlocked.CompareExchange(ref _isCheckingUpdate, 0, 0) == 1;

    /// <summary>更新チェックの進行状態が変化したときに発火</summary>
    public static event Action<bool>? UpdateCheckStateChanged;

    private static bool TryBeginUpdateCheck()
    {
        if (Interlocked.CompareExchange(ref _isCheckingUpdate, 1, 0) != 0)
            return false;
        try { UpdateCheckStateChanged?.Invoke(true); } catch { }
        return true;
    }

    private static void EndUpdateCheck()
    {
        if (Interlocked.CompareExchange(ref _isCheckingUpdate, 0, 1) == 1)
            try { UpdateCheckStateChanged?.Invoke(false); } catch { }
    }

    /// <summary>
    /// Velopack 自動更新チェック + ダイアログ表示
    /// </summary>
    /// <param name="manually">true: 手動チェック (UpToDate でもダイアログ表示), false: 自動チェック</param>
    public static void Check4Update(bool manually = false)
    {
        if (!TryBeginUpdateCheck()) return;

        try
        {
            var op = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    var settings = SettingsManager.Instance.CreateSnapshot();
                    var built = UpdateChecker.TryBuildUpdateManager(settings);
                    if (built is null)
                    {
                        if (manually)
                            await MessageService.ShowInfo(Text("Update.RepoNotConfigured"));
                        return;
                    }
                    var (mgr, baseUrl, channel) = built.Value;

                    if (!mgr.IsInstalled)
                    {
                        Logger.Log("Velopack の IsInstalled=false のため自動更新チェックをスキップ", LogLevel.Warning);
                        if (manually)
                            await MessageService.ShowInfo(Text("Update.DevSkip"));
                        return;
                    }

                    var options = new VelopackUpdateDialog.UpdateDialogOptions
                    {
                        Strings = Models.VStoVSCUpdateStrings.Instance,
                        IgnoredTagName = SettingsManager.Instance.Current.IgnoreUpdateTag,
                    };
                    options.VersionIgnored += tag =>
                        Dispatcher.UIThread.Post(() =>
                        {
                            try { SettingsManager.Instance.MutateAndSave(s => s.IgnoreUpdateTag = tag); }
                            catch (Exception saveEx) { Logger.LogException("IgnoreUpdateTag の保存に失敗", saveEx); }
                        });
                    options.ErrorOccurred += ex =>
                        Logger.Log($"Velopack 更新失敗: {ex.GetType().Name}: {ex.Message}", LogLevel.Warning);

                    var owner = (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                    Logger.Log($"Velopack 自動更新チェック開始: manually={manually}, baseUrl={baseUrl}, channel={channel}");

                    CancellationToken cancelToken = CancellationToken.None;
                    CancellationTokenSource? autoCts = null;
                    if (!manually)
                    {
                        autoCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        cancelToken = autoCts.Token;
                    }

                    try
                    {
                        await VelopackUpdateDialog.UpdateDialogWindow.ShowAsync(
                            owner, mgr, options, manualCheck: manually, cancelToken);
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.Log($"自動更新チェックがタイムアウトしました", LogLevel.Warning);
                    }
                    finally
                    {
                        autoCts?.Dispose();
                        (mgr as IDisposable)?.Dispose();
                    }

                    Logger.Log($"Velopack 自動更新チェック完了: manually={manually}");
                }
                catch (Exception e)
                {
                    Logger.LogException("更新チェック失敗", e);
                }
                finally
                {
                    EndUpdateCheck();
                }
            });

            _ = op.ContinueWith(t =>
            {
                if (IsUpdateCheckInProgress)
                {
                    EndUpdateCheck();
                    if (t.IsFaulted)
                        Logger.LogException("Check4Update の DispatcherOperation が異常終了", t.Exception!);
                }
            }, TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            EndUpdateCheck();
            Logger.LogException("Check4Update の InvokeAsync 呼び出しに失敗", ex);
        }
    }
}
