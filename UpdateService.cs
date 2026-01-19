using System.Diagnostics;
using Velopack;

namespace VS_to_VSC
{
    /// <summary>
    /// Velopack の更新チェックを担当するサービス
    /// </summary>
    public sealed class UpdateService
    {
        private const string UpdateUrlEnvironmentKey = "VELOPACK_UPDATE_URL";

        /// <summary>
        /// 更新の有無を確認し、必要に応じて再起動する
        /// </summary>
        public async Task TryUpdateAsync()
        {
            var updateUrl = Environment.GetEnvironmentVariable(UpdateUrlEnvironmentKey);
            if (string.IsNullOrWhiteSpace(updateUrl))
            {
                Debug.WriteLine("Velopack 更新 URL が未設定のため、自動更新をスキップします。");
                return;
            }

            try
            {
                var updateManager = new UpdateManager(updateUrl);
                var updateInfo = await updateManager.CheckForUpdatesAsync();
                if (updateInfo is null)
                {
                    return;
                }

                await updateManager.DownloadUpdatesAsync(updateInfo);
                updateManager.ApplyUpdatesAndRestart(updateInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Velopack 更新チェックで例外が発生しました: {ex.Message}");
            }
        }
    }
}
