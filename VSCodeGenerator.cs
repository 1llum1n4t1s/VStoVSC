using Microsoft.Build.Construction;
using Microsoft.Build.Locator;
using System.IO;
using System.Text.Json;

namespace VS_to_VSC;

/// <summary>
/// VSCode設定ファイル生成を担当するクラス
/// </summary>
public partial class VSCodeGenerator
{
    #region 定数定義

    // Visual Studio関連の定数
    private const string VisualStudioPath = @"C:\Program Files\Microsoft Visual Studio\2022\Professional";
    private const string MSBuildPath = @"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe";
    private const int MinimumVisualStudioVersion = 17; // Visual Studio 2022以降
    private const string VSToolsPathTemplate = "MSBuild/Microsoft/VisualStudio/v17.0";

    // 設定ファイル名
    private const string TasksJsonFileName = "tasks.json";
    private const string VSCodeDirectoryName = ".vscode";

    // JSON設定
    private const string TasksVersion = "2.0.0";

    #endregion

    #region フィールド

    /// <summary>
    /// ログ出力用のコールバック
    /// </summary>
    private readonly Action<string> _logCallback;

    /// <summary>
    /// JSON シリアライゼーション用のオプション（キャッシュ用）
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    #endregion

    #region コンストラクタ

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="logCallback">ログ出力用のコールバック（省略時はデフォルトのDebug.WriteLineを使用）</param>
    public VSCodeGenerator(Action<string>? logCallback = null)
    {
        // ログ出力用のコールバックを設定（省略時はデフォルトのDebug.WriteLineを使用）
        _logCallback = logCallback ?? (message => System.Diagnostics.Debug.WriteLine(message));

        // MSBuildの初期化
        InitializeMSBuild();
    }

    #endregion

    #region プライベートメソッド

    /// <summary>
    /// ログメッセージを出力する
    /// </summary>
    /// <param name="message">出力するメッセージ</param>
    private void LogMessage(string message)
    {
        _logCallback(message);
    }

    /// <summary>
    /// MSBuildの初期化を行う
    /// </summary>
    private void InitializeMSBuild()
    {
        try
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances();

            // Visual Studio付属のMSBuildインスタンスを優先的に検索
            var visualStudioInstance = FindBestVisualStudioInstance(instances);

            if (visualStudioInstance != null)
            {
                RegisterVisualStudioInstance(visualStudioInstance);
            }
            else
            {
                HandleFallbackMSBuildSetup(instances);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"MSBuild初期化中にエラーが発生: {ex.Message}");
        }
    }

    /// <summary>
    /// 最適なVisual Studioインスタンスを検索する
    /// </summary>
    /// <param name="instances">MSBuildインスタンスのコレクション</param>
    /// <returns>最適なVisual Studioインスタンス</returns>
    private static VisualStudioInstance? FindBestVisualStudioInstance(IEnumerable<VisualStudioInstance> instances)
    {
        return instances.FirstOrDefault(x =>
            x.DiscoveryType == DiscoveryType.VisualStudioSetup &&
            x.Version.Major >= MinimumVisualStudioVersion);
    }

    /// <summary>
    /// Visual Studioインスタンスを登録し、環境変数を設定する
    /// </summary>
    /// <param name="visualStudioInstance">Visual Studioインスタンス</param>
    private void RegisterVisualStudioInstance(VisualStudioInstance visualStudioInstance)
    {
        MSBuildLocator.RegisterInstance(visualStudioInstance);

        var vsPath = visualStudioInstance.VisualStudioRootPath;
        Environment.SetEnvironmentVariable("VSINSTALLDIR", vsPath);
        Environment.SetEnvironmentVariable("VSToolsPath", Path.Combine(vsPath, VSToolsPathTemplate));

        LogMessage($"Visual Studio MSBuildインスタンスを登録: {vsPath}");
    }

    /// <summary>
    /// フォールバック用のMSBuildセットアップを処理する
    /// </summary>
    /// <param name="instances">MSBuildインスタンスのコレクション</param>
    private void HandleFallbackMSBuildSetup(IEnumerable<VisualStudioInstance> instances)
    {
        if (File.Exists(MSBuildPath))
        {
            SetupManualMSBuildEnvironment();
            RegisterBestAvailableInstance(instances);
        }
        else
        {
            RegisterLatestInstance(instances);
        }
    }

    /// <summary>
    /// 手動でMSBuild環境を設定する
    /// </summary>
    private static void SetupManualMSBuildEnvironment()
    {
        Environment.SetEnvironmentVariable("VSINSTALLDIR", VisualStudioPath);
        Environment.SetEnvironmentVariable("VSToolsPath", Path.Combine(VisualStudioPath, VSToolsPathTemplate));
    }

    /// <summary>
    /// 利用可能な最適なインスタンスを登録する
    /// </summary>
    /// <param name="instances">MSBuildインスタンスのコレクション</param>
    private void RegisterBestAvailableInstance(IEnumerable<VisualStudioInstance> instances)
    {
        var bestInstance = instances
            .Where(x => x.DiscoveryType == DiscoveryType.VisualStudioSetup)
            .OrderByDescending(x => x.Version)
            .FirstOrDefault();

        if (bestInstance != null)
        {
            MSBuildLocator.RegisterInstance(bestInstance);
            LogMessage($"最適なMSBuildインスタンスを登録: {bestInstance.Name}");
        }
        else
        {
            RegisterLatestInstance(instances);
        }
    }

    /// <summary>
    /// 最新のインスタンスを登録する
    /// </summary>
    /// <param name="instances">MSBuildインスタンスのコレクション</param>
    private void RegisterLatestInstance(IEnumerable<VisualStudioInstance> instances)
    {
        if (instances.Any())
        {
            var instance = instances.OrderByDescending(x => x.Version).First();
            MSBuildLocator.RegisterInstance(instance);
            LogMessage($"最新のMSBuildインスタンスを登録: {instance.Name}");
        }
    }

    #endregion

    #region パブリックメソッド

    /// <summary>
    /// 共通のタスクプレゼンテーション設定を取得する
    /// </summary>
    /// <returns>プレゼンテーション設定オブジェクト</returns>
    private static object GetTaskPresentation()
    {
        return new
        {
            echo = true,
            reveal = "always",
            focus = false,
            panel = "shared",
            showReuseMessage = true,
            clear = false
        };
    }

    /// <summary>
    /// MSBuildタスクを作成する
    /// </summary>
    /// <param name="solutionName">ソリューション名</param>
    /// <param name="solutionFileName">ソリューションファイル名（拡張子付き）</param>
    /// <param name="taskType">タスクタイプ（Build/Clean/Rebuild）</param>
    /// <param name="configuration">ビルド構成（Buildタスクの場合のみ）</param>
    /// <returns>タスクオブジェクト</returns>
    private static object CreateMSBuildTask(
        string solutionName,
        string solutionFileName,
        string taskType,
        string? configuration = null)
    {
        var (label, args, detail, isDefault) = taskType switch
        {
            "Build" => (
                $"ビルド - {solutionName} ソリューション - {configuration}",
                new[] { solutionFileName, $"/p:Configuration={configuration}", "/verbosity:normal" },
                $"Visual Studio 2022 MSBuildを使用して{solutionName}ソリューション全体を{configuration}構成でビルド",
                configuration == "Debug"
            ),
            "Clean" => (
                $"クリーン - {solutionName} ソリューション",
                new[] { solutionFileName, "/t:Clean", "/verbosity:normal" },
                $"Visual Studio 2022 MSBuildを使用して{solutionName}ソリューション全体をクリーン",
                false
            ),
            "Rebuild" => (
                $"リビルド - {solutionName} ソリューション",
                new[] { solutionFileName, "/t:Rebuild", "/verbosity:normal" },
                $"Visual Studio 2022 MSBuildを使用して{solutionName}ソリューション全体をリビルド",
                false
            ),
            _ => throw new ArgumentException($"不明なタスクタイプ: {taskType}")
        };

        return new
        {
            label,
            type = "shell",
            command = MSBuildPath,
            args,
            group = new { kind = "build", isDefault },
            presentation = GetTaskPresentation(),
            problemMatcher = "$msCompile",
            detail
        };
    }

    /// <summary>
    /// tasks.jsonファイルを生成する
    /// </summary>
    /// <param name="vscodeDir">.vscodeディレクトリのパス</param>
    /// <param name="solutionName">ソリューション名</param>
    /// <param name="solutionFileName">ソリューションファイル名（拡張子付き）</param>
    public void GenerateTasksJson(string vscodeDir, string solutionName, string solutionFileName)
    {
        List<object> tasks = [
            CreateMSBuildTask(solutionName, solutionFileName, "Build", "Debug"),
            CreateMSBuildTask(solutionName, solutionFileName, "Build", "Release"),
            CreateMSBuildTask(solutionName, solutionFileName, "Clean"),
            CreateMSBuildTask(solutionName, solutionFileName, "Rebuild")
        ];

        var tasksJson = new
        {
            version = TasksVersion,
            tasks
        };

        var tasksPath = Path.Combine(vscodeDir, TasksJsonFileName);
        SaveJsonFile(tasksPath, tasksJson);

        LogMessage($"tasks.json生成完了: {tasks.Count}個のタスク");
    }

    /// <summary>
    /// VSCode設定ファイルを生成する
    /// </summary>
    /// <param name="solutionPath">元のソリューションファイルのパス</param>
    public void GenerateVSCodeFiles(string solutionPath)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath)!;
        var solutionName = Path.GetFileNameWithoutExtension(solutionPath);
        var solutionFileName = Path.GetFileName(solutionPath);
        var vscodeDir = Path.Combine(solutionDir, VSCodeDirectoryName);

        // 既存の.vscodeフォルダを削除して再作成
        if (Directory.Exists(vscodeDir))
        {
            Directory.Delete(vscodeDir, true);
            LogMessage("既存の.vscodeフォルダを削除しました。");
        }

        Directory.CreateDirectory(vscodeDir);
        LogMessage("新しい.vscodeフォルダを作成しました。");

        // tasks.jsonファイルを生成
        GenerateTasksJson(vscodeDir, solutionName, solutionFileName);

        LogMessage("VSCode設定ファイル生成が完了しました。");
    }

    /// <summary>
    /// JSONファイルを保存する
    /// </summary>
    /// <param name="filePath">保存するファイルのパス</param>
    /// <param name="obj">保存するオブジェクト</param>
    private static void SaveJsonFile(string filePath, object obj)
    {
        var jsonString = JsonSerializer.Serialize(obj, JsonOptions);
        File.WriteAllText(filePath, jsonString, System.Text.Encoding.UTF8);
    }

    #endregion
}
