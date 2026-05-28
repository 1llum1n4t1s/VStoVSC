using Velopack;
using Velopack.Sources;

namespace VStoVSC.Util;

/// <summary>
/// アプリケーションの更新チェック・ダウンロードを行う共通クラス。
/// Velopack の SimpleWebSource ベースで Cloudflare R2 (vs2vsc.nephilim.jp) から取得する。
/// </summary>
public static class UpdateChecker
{
    private const int CheckTimeoutMs = 10000;
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);

    /// <summary>更新チェック結果の種類</summary>
    public enum UpdateResult
    {
        NoUpdate,
        Downloaded,
        NotInstalled,
        NotConfigured,
        Error
    }

    /// <summary>更新チェック結果</summary>
    public record CheckResult(UpdateResult Result, UpdateInfo? Info, UpdateManager? Manager, string Message);

    /// <summary>
    /// <see cref="Settings"/> から <see cref="UpdateManager"/> を組み立てる共通ファクトリ。
    /// </summary>
    internal static (UpdateManager Manager, string BaseUrl, string Channel)? TryBuildUpdateManager(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var baseUrl = settings.UpdateBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        var channel = string.IsNullOrWhiteSpace(settings.UpdateChannel) ? "win" : settings.UpdateChannel;
        var source = new SimpleWebSource(baseUrl);
        // CodeRabbit #3312176160 対応: Velopack 1.0.x の UpdateManager.CheckForUpdatesAsync は引数で channel を取れないため
        // UpdateOptions.ExplicitChannel で明示する。これがないと installed channel に依存し、設定変更が反映されない。
        var options = new UpdateOptions { ExplicitChannel = channel };
        return (new UpdateManager(source, options), baseUrl, channel);
    }

    /// <summary>
    /// 更新を確認し、利用可能であればダウンロードまで行う。
    /// 適用は呼び出し元で行う (ApplyUpdatesAndRestart / WaitExitThenApplyUpdates)。
    /// </summary>
    public static async Task<CheckResult> CheckAndDownloadAsync(IProgress<string>? statusProgress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = SettingsManager.Instance.CreateSnapshot();
            var build = TryBuildUpdateManager(settings);
            if (build is null)
            {
                Logger.Log("更新元リポジトリが未設定のため更新チェックをスキップします。");
                return new CheckResult(UpdateResult.NotConfigured, null, null, App.Text("Update.RepoNotConfigured"));
            }
            var (updateManager, baseUrl, channel) = build.Value;

            if (!updateManager.IsInstalled)
            {
                Logger.Log("開発実行のため更新チェックをスキップします。");
                return new CheckResult(UpdateResult.NotInstalled, null, null, App.Text("Update.DevSkip"));
            }

            Logger.Log($"更新チェック: 配信元: {baseUrl}, チャンネル: {channel}");
            statusProgress?.Report(App.Text("Update.Checking"));

            UpdateInfo? updateInfo;
            try
            {
                var checkTask = updateManager.CheckForUpdatesAsync();
                var timeoutTask = Task.Delay(CheckTimeoutMs, cancellationToken);
                var completedTask = await Task.WhenAny(checkTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Logger.Log(App.Text("Update.Timeout"));
                    return new CheckResult(UpdateResult.Error, null, null, App.Text("Update.Timeout"));
                }

                updateInfo = await checkTask;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Logger.Log(App.Text("Update.Timeout"));
                return new CheckResult(UpdateResult.Error, null, null, App.Text("Update.Timeout"));
            }

            if (updateInfo == null)
            {
                Logger.Log("利用可能な更新はありません。");
                return new CheckResult(UpdateResult.NoUpdate, null, null, App.Text("Update.Latest"));
            }

            Logger.Log("新しいバージョンを検出しました。更新をダウンロードしています...");
            statusProgress?.Report(App.Text("Update.Downloading"));

            try
            {
                using var downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                downloadCts.CancelAfter(DownloadTimeout);
                await updateManager.DownloadUpdatesAsync(updateInfo, null, downloadCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Logger.Log("ダウンロードがタイムアウトしました。", LogLevel.Warning);
                return new CheckResult(UpdateResult.Error, null, null, App.Text("Update.DownloadTimeout"));
            }

            Logger.Log("ダウンロード完了。更新の適用準備ができました。");
            return new CheckResult(UpdateResult.Downloaded, updateInfo, updateManager, App.Text("Update.Downloaded"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogException("更新チェック中にエラーが発生しました", ex);
            return new CheckResult(UpdateResult.Error, null, null, App.Text("Update.CheckError"));
        }
    }
}
