using Microsoft.Build.Construction;
using Microsoft.Build.Locator;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace VS_to_VSC;

/// <summary>
/// VSCode設定ファイル生成を担当するクラス
/// </summary>
public partial class VSCodeGenerator
{
    #region 定数定義

    // 設定ファイル名
    private const string TasksJsonFileName = "tasks.json";
    private const string VSCodeDirectoryName = ".vscode";
    private const string TasksVersion = "2.0.0";
    private const int MinimumVisualStudioVersion = 10; // Visual Studio 2010以降

    #endregion

    #region フィールド

    /// <summary>
    /// ログ出力用のコールバック
    /// </summary>
    private readonly Action<string> _logCallback;

    /// <summary>
    /// 検出されたVisual Studioのパス
    /// </summary>
    private string _visualStudioPath = string.Empty;

    /// <summary>
    /// 検出されたMSBuildパス
    /// </summary>
    private string _msbuildPath = string.Empty;

    /// <summary>
    /// 使用するMSBuildの実行パス
    /// </summary>
    private string _msbuildExecutablePath = string.Empty;

    /// <summary>
    /// 検出されたVisual Studioのバージョン
    /// </summary>
    private string _visualStudioVersion = string.Empty;

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

            if (!instances.Any())
            {
                LogMessage("Visual Studioのインスタンスが見つかりません。デフォルトMSBuildパスを使用します。");
                UseDefaultMSBuildPath();
                return;
            }

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

            LogMessage($"使用するMSBuildパス: {_msbuildExecutablePath}");
        }
        catch (Exception ex)
        {
            LogMessage($"MSBuild初期化中にエラーが発生: {ex.Message}");
            UseDefaultMSBuildPath();
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
        _visualStudioPath = vsPath;
        _visualStudioVersion = GetVisualStudioVersionName(visualStudioInstance.Version);
        Environment.SetEnvironmentVariable("VSINSTALLDIR", vsPath);

        const string vsToolsPathTemplate = "MSBuild/Microsoft/VisualStudio/v17.0";
        Environment.SetEnvironmentVariable("VSToolsPath", Path.Combine(vsPath, vsToolsPathTemplate));

        SetMSBuildExecutablePath(visualStudioInstance.MSBuildPath);
        LogMessage($"Visual Studio MSBuildインスタンスを登録: {vsPath}");
    }

    /// <summary>
    /// フォールバック用のMSBuildセットアップを処理する
    /// </summary>
    /// <param name="instances">MSBuildインスタンスのコレクション</param>
    private void HandleFallbackMSBuildSetup(IEnumerable<VisualStudioInstance> instances)
    {
        RegisterLatestInstance(instances);
        if (string.IsNullOrEmpty(_msbuildExecutablePath))
        {
            UseDefaultMSBuildPath();
        }
    }

    /// <summary>
    /// Visual Studioのバージョン番号から名前を取得する
    /// </summary>
    /// <param name="version">Visual Studioのバージョン</param>
    /// <returns>Visual Studioの名前（例：Visual Studio 2026）</returns>
    private static string GetVisualStudioVersionName(Version version)
    {
        return version.Major switch
        {
            10 => "Visual Studio 2010",
            11 => "Visual Studio 2012",
            12 => "Visual Studio 2013",
            14 => "Visual Studio 2015",
            15 => "Visual Studio 2017",
            16 => "Visual Studio 2019",
            17 => "Visual Studio 2022",
            18 => "Visual Studio 2026",
            _ => $"Visual Studio (Version {version.Major})"
        };
    }

    /// <summary>
    /// デフォルトのMSBuildパスを使用するように設定する（インスタンスが見つからない場合の代替処理）
    /// </summary>
    private void UseDefaultMSBuildPath()
    {
        var programFilesPath = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        var visualStudioBasePath = Path.Combine(programFilesPath, "Microsoft Visual Studio");

        if (!Directory.Exists(visualStudioBasePath))
        {
            LogMessage("Visual Studioのインストールディレクトリが見つかりません。");
            return;
        }

        // インストールされているVisual Studioのバージョンを検索（降順で最新のものから取得）
        var vsVersionDirs = Directory.GetDirectories(visualStudioBasePath)
            .Select(d => Path.GetFileName(d))
            .OrderByDescending(d => d)
            .ToList();

        if (!vsVersionDirs.Any())
        {
            LogMessage("利用可能なVisual Studioが見つかりません。");
            return;
        }

        // 見つかったバージョンごとに、エディション（Professional/Enterprise/Community）を検索
        foreach (var vsVersion in vsVersionDirs)
        {
            var vsVersionPath = Path.Combine(visualStudioBasePath, vsVersion);
            var editions = new[] { "Professional", "Enterprise", "Community" };

            foreach (var edition in editions)
            {
                var editionPath = Path.Combine(vsVersionPath, edition);
                var msbuildPath = Path.Combine(editionPath, "MSBuild", "Current", "Bin", "MSBuild.exe");

                if (File.Exists(msbuildPath))
                {
                    _visualStudioPath = editionPath;
                    _msbuildPath = msbuildPath;
                    _msbuildExecutablePath = msbuildPath;
                    if (int.TryParse(vsVersion, out var versionNumber))
                    {
                        _visualStudioVersion = GetVisualStudioVersionName(new Version(versionNumber, 0));
                    }
                    else
                    {
                        _visualStudioVersion = $"Visual Studio {vsVersion}";
                    }
                    SetupManualMSBuildEnvironment();
                    LogMessage($"最新のVisual Studioを検出: {vsVersion} {edition}");
                    return;
                }
            }
        }

        LogMessage("MSBuild.exeが見つかりません。");
    }

    /// <summary>
    /// 手動でMSBuild環境を設定する
    /// </summary>
    private void SetupManualMSBuildEnvironment()
    {
        const string vsToolsPathTemplate = "MSBuild/Microsoft/VisualStudio/v17.0";
        Environment.SetEnvironmentVariable("VSINSTALLDIR", _visualStudioPath);
        Environment.SetEnvironmentVariable("VSToolsPath", Path.Combine(_visualStudioPath, vsToolsPathTemplate));
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
            _visualStudioVersion = GetVisualStudioVersionName(bestInstance.Version);
            SetMSBuildExecutablePath(bestInstance.MSBuildPath);
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
            _visualStudioVersion = GetVisualStudioVersionName(instance.Version);
            SetMSBuildExecutablePath(instance.MSBuildPath);
            LogMessage($"最新のMSBuildインスタンスを登録: {instance.Name}");
        }
    }

    /// <summary>
    /// MSBuild実行パスを設定する
    /// </summary>
    /// <param name="msbuildPath">MSBuildパス</param>
    private void SetMSBuildExecutablePath(string msbuildPath)
    {
        if (File.Exists(msbuildPath))
        {
            _msbuildExecutablePath = msbuildPath;
            return;
        }

        var candidate = Path.Combine(msbuildPath, "MSBuild.exe");
        if (File.Exists(candidate))
        {
            _msbuildExecutablePath = candidate;
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
    private object CreateMSBuildTask(
        string solutionName,
        string solutionFileName,
        string taskType,
        string? configuration = null)
    {
        var vsVersionText = string.IsNullOrEmpty(_visualStudioVersion) ? "Visual Studio" : _visualStudioVersion;
        var (label, args, detail, isDefault) = taskType switch
        {
            "Build" => (
                $"ビルド - {solutionName} ソリューション - {configuration}",
                new[] { solutionFileName, $"/p:Configuration={configuration}", "/verbosity:normal" },
                $"{vsVersionText} MSBuildを使用して{solutionName}ソリューション全体を{configuration}構成でビルド",
                configuration == "Debug"
            ),
            "Clean" => (
                $"クリーン - {solutionName} ソリューション",
                new[] { solutionFileName, "/t:Clean", "/verbosity:normal" },
                $"{vsVersionText} MSBuildを使用して{solutionName}ソリューション全体をクリーン",
                false
            ),
            "Rebuild" => (
                $"リビルド - {solutionName} ソリューション",
                new[] { solutionFileName, "/t:Rebuild", "/verbosity:normal" },
                $"{vsVersionText} MSBuildを使用して{solutionName}ソリューション全体をリビルド",
                false
            ),
            _ => throw new ArgumentException($"不明なタスクタイプ: {taskType}")
        };

        return new
        {
            label,
            type = "shell",
            command = _msbuildExecutablePath,
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

        // 既存の.vscodeフォルダがある場合は確認ダイアログを表示
        if (Directory.Exists(vscodeDir))
        {
            var result = MessageBox.Show(
                "既存の.vscodeフォルダが見つかりました。\n削除して再生成しますか？\n\n「削除しない」を選ぶと tasks.json のみ上書きします。",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Directory.Delete(vscodeDir, true);
                LogMessage("既存の.vscodeフォルダを削除しました。");
                Directory.CreateDirectory(vscodeDir);
                LogMessage("新しい.vscodeフォルダを作成しました。");
            }
            else
            {
                LogMessage("既存の.vscodeフォルダを保持し、tasks.jsonのみ上書きします。");
            }
        }
        else
        {
            Directory.CreateDirectory(vscodeDir);
            LogMessage("新しい.vscodeフォルダを作成しました。");
        }

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
