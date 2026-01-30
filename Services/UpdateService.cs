using System.Diagnostics;
using System.Runtime.Versioning;
using Windows.Services.Store;

namespace VS_to_VSC.Services;

/// <summary>
/// Microsoft Store の更新チェックおよび必須更新の適用を担当するサービス
/// </summary>
public sealed class UpdateService
{
    /// <summary>
    /// 更新の有無を確認し、必須更新がある場合はダウンロード・インストールを実行する
    /// </summary>
    [SupportedOSPlatform("windows10.0.26100.0")]
    public async Task TryUpdateAsync()
    {
        try
        {
            var context = StoreContext.GetDefault();
            var updates = await context.GetAppAndOptionalStorePackageUpdatesAsync();
            if (updates.Count == 0)
            {
                return;
            }

            var hasMandatory = false;
            foreach (StorePackageUpdate update in updates)
            {
                if (update.Mandatory)
                {
                    hasMandatory = true;
                    break;
                }
            }

            if (!hasMandatory)
            {
                return;
            }

            _ = await context.RequestDownloadAndInstallStorePackageUpdatesAsync(updates);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Microsoft Store 更新チェックで例外が発生しました: {ex.Message}");
        }
    }
}
