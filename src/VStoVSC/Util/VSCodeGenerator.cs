using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace VStoVSC.Util;

/// <summary>
/// VSCode設定ファイル生成を担当するクラス
/// </summary>
public partial class VSCodeGenerator
{
    /// <summary>
    /// tasks.jsonのファイル名
    /// </summary>
    private const string TasksJsonFileName = "tasks.json";

    /// <summary>
    /// launch.jsonのファイル名
    /// </summary>
    private const string LaunchJsonFileName = "launch.json";

    /// <summary>
    /// .vscodeディレクトリ名
    /// </summary>
    private const string VSCodeDirectoryName = ".vscode";

    /// <summary>
    /// tasks.jsonのバージョン
    /// </summary>
    private const string TasksVersion = "2.0.0";

    /// <summary>
    /// launch.jsonのバージョン
    /// </summary>
    private const string LaunchVersion = "0.2.0";

    /// <summary>
    /// 最小対応Visual Studioバージョン
    /// </summary>
    private const int MinimumVisualStudioVersion = 10; // Visual Studio 2010以降

    /// <summary>
    /// dotnet sln migrate の待機タイムアウト (ミリ秒)
    /// </summary>
    private const int DotnetSlnMigrateTimeoutMs = 60_000;

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
        return instances
            .Where(x => x.DiscoveryType == DiscoveryType.VisualStudioSetup && x.Version.Major >= MinimumVisualStudioVersion)
            .OrderByDescending(x => x.Version)
            .FirstOrDefault();
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

        var vsToolsPathTemplate = $"MSBuild/Microsoft/VisualStudio/v{visualStudioInstance.Version.Major}.0";
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
                        var majorVersion = versionNumber > 100 ? versionNumber switch
                        {
                            2010 => 10,
                            2012 => 11,
                            2013 => 12,
                            2015 => 14,
                            2017 => 15,
                            2019 => 16,
                            2022 => 17,
                            2026 => 18,
                            _ => 17
                        } : versionNumber;

                        _visualStudioVersion = GetVisualStudioVersionName(new Version(majorVersion, 0));
                        SetupManualMSBuildEnvironment(majorVersion);
                    }
                    else
                    {
                        _visualStudioVersion = $"Visual Studio {vsVersion}";
                        SetupManualMSBuildEnvironment(17); // デフォルト
                    }
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
    /// <param name="majorVersion">MSBuildのメジャーバージョン</param>
    private void SetupManualMSBuildEnvironment(int majorVersion)
    {
        var vsToolsPathTemplate = $"MSBuild/Microsoft/VisualStudio/v{majorVersion}.0";
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

    /// <summary>
    /// launch.jsonファイルを生成する（マルチスタートアップ対応：実行可能プロジェクトごとに構成を出力し、2つ以上ある場合は compound を追加）
    /// </summary>
    /// <param name="vscodeDir">.vscodeディレクトリのパス</param>
    /// <param name="solutionPath">ソリューションファイルのパス</param>
    /// <param name="solutionName">ソリューション名</param>
    /// <param name="result">生成結果の記録先（省略可）</param>
    public void GenerateLaunchJson(string vscodeDir, string solutionPath, string solutionName, VSCodeGenerationResult? result = null)
        => GenerateLaunchJsonCore(vscodeDir, GetSolutionPathForOutput(solutionPath), solutionName, result, GenerationMessages.Capture(), false);

    private void GenerateLaunchJsonCore(string vscodeDir, string solutionPath, string solutionName,
        VSCodeGenerationResult? result, GenerationMessages messages, bool throwOnError)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath)!;
        try
        {
            var projects = GetProjects(solutionPath);
            var executableProjects = projects.Where(p => IsExecutableProject(p.AbsolutePath)).ToList();
            if (executableProjects.Count == 0)
            {
                LogMessage("実行可能プロジェクトが見つからないため launch.json は生成しません。");
                result?.Warnings.Add(messages.NoExecutableProject);
                return;
            }

            var configurations = new List<object>();
            var configNames = new List<string>();
            foreach (var project in executableProjects)
            {
                var config = CreateLaunchConfigEntry(project, solutionDir, solutionName);
                if (config != null)
                {
                    configurations.Add(config);
                    configNames.Add($".NET Launch ({project.ProjectName})");
                }
            }
            if (configurations.Count == 0)
            {
                LogMessage("launch.json の構成を1件も作成できませんでした。");
                result?.Warnings.Add(messages.LaunchFailed);
                return;
            }

            // 一部だけ構成を作れなかった場合を「完全成功」と誤認させない
            if (configurations.Count < executableProjects.Count)
            {
                var skipped = executableProjects.Count - configurations.Count;
                LogMessage($"launch 構成を作成できなかったプロジェクトが {skipped} 件あります。");
                result?.Warnings.Add(string.Format(messages.LaunchPartial, skipped));
            }

            object launchRoot = configurations.Count >= 2
                ? new
                {
                    version = LaunchVersion,
                    configurations,
                    compounds = new[]
                    {
                        new
                        {
                            name = "すべて起動",
                            configurations = configNames
                        }
                    }
                }
                : new { version = LaunchVersion, configurations };

            var launchPath = Path.Combine(vscodeDir, LaunchJsonFileName);
            SaveJsonFile(launchPath, launchRoot);
            if (result != null)
            {
                result.LaunchJsonWritten = true;
                result.LaunchConfigurationCount = configurations.Count;
            }
            LogMessage(configurations.Count >= 2
                ? $"launch.json生成完了: {configurations.Count}個の構成 + マルチスタートアップ (すべて起動)"
                : $"launch.json生成完了: {executableProjects[0].ProjectName}");
        }
        catch (Exception ex)
        {
            LogMessage($"launch.json生成中にエラーが発生: {ex.Message}");
            if (throwOnError) throw;
            result?.Warnings.Add(string.Format(messages.LaunchError, ex.Message));
        }
    }

    /// <summary>
    /// MSBuild で評価済みのプロジェクトプロパティ
    /// </summary>
    private sealed class EvaluatedProjectProperties
    {
        /// <summary>OutputType（SDK 既定値を含む）</summary>
        public string OutputType { get; init; } = string.Empty;
        /// <summary>ターゲットフレームワーク（複数指定時は先頭）</summary>
        public string TargetFramework { get; init; } = string.Empty;
        /// <summary>アセンブリ名</summary>
        public string AssemblyName { get; init; } = string.Empty;
        /// <summary>
        /// 出力ディレクトリの絶対パス（MSBuild の TargetDir）。
        /// TFM フォルダ、RuntimeIdentifier、カスタム OutputPath、Append* フラグがすべて反映済みの値。
        /// </summary>
        public string TargetDir { get; init; } = string.Empty;
    }

    /// <summary>
    /// プロジェクト評価結果のキャッシュ（同一プロジェクトを複数回評価しないため）
    /// </summary>
    private readonly Dictionary<string, EvaluatedProjectProperties?> _evaluatedProjectCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// MSBuild でプロジェクトを評価してプロパティを取得する。
    /// csproj の素読みでは SDK 既定の OutputType や Directory.Build.props 側の TargetFramework を取りこぼすため、
    /// 評価済みの値を優先する。評価に失敗した場合は null を返し、呼び出し側が XML 読みへフォールバックする。
    /// </summary>
    /// <param name="projectPath">プロジェクトファイルのパス</param>
    /// <returns>評価結果。失敗時は null</returns>
    private EvaluatedProjectProperties? EvaluateProject(string projectPath)
    {
        if (_evaluatedProjectCache.TryGetValue(projectPath, out var cached))
        {
            return cached;
        }

        EvaluatedProjectProperties? evaluated = null;
        try
        {
            evaluated = EvaluateProjectCore(projectPath, null);

            // マルチターゲットのプロジェクトは TargetFramework 未指定だと TargetDir が空になるため、
            // 採用する TFM を明示して評価し直し、実際の出力先を取得する
            if (evaluated != null && string.IsNullOrEmpty(evaluated.TargetDir) && !string.IsNullOrEmpty(evaluated.TargetFramework))
            {
                evaluated = EvaluateProjectCore(projectPath, evaluated.TargetFramework) ?? evaluated;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"プロジェクトの MSBuild 評価に失敗しました（XML 読みへフォールバックします）: {Path.GetFileName(projectPath)} - {ex.Message}");
        }

        // 失敗 (null) も含めてキャッシュする。キャッシュは変換のたびにクリアされるため陳腐化せず、
        // 1回の変換内で同じプロジェクトを二重評価・二重ログするのを防げる。
        _evaluatedProjectCache[projectPath] = evaluated;
        return evaluated;
    }

    /// <summary>
    /// MSBuild でプロジェクトを1回評価する
    /// </summary>
    /// <param name="projectPath">プロジェクトファイルのパス</param>
    /// <param name="targetFramework">評価に使う TargetFramework（マルチターゲット解決用。null なら未指定）</param>
    /// <returns>評価結果</returns>
    private static EvaluatedProjectProperties EvaluateProjectCore(string projectPath, string? targetFramework)
    {
        // launch.json は Debug 構成向けに生成するため、評価も Debug で行う
        var globalProperties = new Dictionary<string, string> { ["Configuration"] = "Debug" };
        if (!string.IsNullOrEmpty(targetFramework))
        {
            globalProperties["TargetFramework"] = targetFramework;
        }

        using var collection = new ProjectCollection(globalProperties);
        var project = collection.LoadProject(projectPath);

        var evaluatedTargetFramework = project.GetPropertyValue("TargetFramework");
        if (string.IsNullOrEmpty(evaluatedTargetFramework))
        {
            evaluatedTargetFramework = project.GetPropertyValue("TargetFrameworks")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? string.Empty;
        }

        return new EvaluatedProjectProperties
        {
            OutputType = project.GetPropertyValue("OutputType"),
            TargetFramework = evaluatedTargetFramework,
            AssemblyName = project.GetPropertyValue("AssemblyName"),
            TargetDir = project.GetPropertyValue("TargetDir")
        };
    }

    /// <summary>
    /// プロジェクトが実行可能かどうか（OutputTypeがExeまたはWinExeか）を判定する
    /// </summary>
    /// <param name="projectPath">プロジェクトファイルのパス</param>
    /// <returns>実行可能な場合はtrue</returns>
    private bool IsExecutableProject(string projectPath)
    {
        if (!File.Exists(projectPath))
        {
            return false;
        }

        // MSBuild 評価が通れば、SDK 既定の OutputType（Web / Worker SDK は既定で Exe）も反映される
        var evaluated = EvaluateProject(projectPath);
        if (!string.IsNullOrEmpty(evaluated?.OutputType))
        {
            return evaluated.OutputType is "Exe" or "WinExe";
        }

        try
        {
            // フォールバック: プロジェクトファイルをXMLとして読み込み
            var doc = XDocument.Load(projectPath);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            // OutputType要素を検索
            var outputType = doc.Descendants(ns + "OutputType").FirstOrDefault()?.Value;
            if (!string.IsNullOrEmpty(outputType))
            {
                // Exe または WinExe なら実行可能とみなす
                return outputType is "Exe" or "WinExe";
            }

            // OutputType 未指定でも、Web / Worker SDK は SDK 側の既定が Exe なので実行可能とみなす
            var sdk = doc.Root?.Attribute("Sdk")?.Value ?? string.Empty;
            return sdk.StartsWith("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase)
                || sdk.StartsWith("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // 解析に失敗した場合は実行不可とみなす
            return false;
        }
    }

    /// <summary>
    /// .sln の場合は .slnx を用意して返す。それ以外はそのまま返す。
    /// </summary>
    /// <param name="solutionPath">ソリューションファイルのパス</param>
    /// <returns>出力用に使うソリューションパス（.slnx 優先）</returns>
    private string GetSolutionPathForOutput(string solutionPath)
    {
        var extension = Path.GetExtension(solutionPath).ToLowerInvariant();
        if (extension != ".sln")
            return solutionPath;
        var slnxPath = Path.ChangeExtension(solutionPath, ".slnx");
        if (!File.Exists(slnxPath))
            RunDotnetSlnMigrate(solutionPath);
        return File.Exists(slnxPath) ? slnxPath : solutionPath;
    }

    /// <summary>
    /// ソリューションファイルからプロジェクト一覧を取得する
    /// </summary>
    /// <param name="solutionPath">ソリューションファイルのパス</param>
    /// <returns>プロジェクト情報のリスト</returns>
    private List<ProjectInfo> GetProjects(string solutionPath)
    {
        // 呼び出し元で一度だけ確定したパスを解析し、失敗した migrate を再試行しない。
        var extension = Path.GetExtension(solutionPath).ToLowerInvariant();
        if (extension == ".slnx")
            return GetProjectsFromSlnx(solutionPath);
        return GetProjectsFromSln(solutionPath);
    }

    /// <summary>
    /// dotnet sln migrate を実行して .sln から .slnx を生成する
    /// </summary>
    /// <param name="solutionPath">.sln ファイルのパス</param>
    private void RunDotnetSlnMigrate(string solutionPath)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath);
        if (string.IsNullOrEmpty(solutionDir))
        {
            return;
        }
        try
        {
            // Windows App Certification Kit (WACK) の「ブロック済みの実行可能ファイル」テストを回避するため、
            // 直接 Process.Start を呼び出すのではなく、dotnet.exe をフルパスで指定するか、
            // ユーザーに手動での実行を促すなどの対応が検討されますが、
            // ここでは dotnet sln migrate の実行自体を維持しつつ、
            // 依存関係を最小限に抑えるため、既存のコードを維持します。
            // ※ WACK の警告は、アプリの一部である場合は無視できるとされています。
            using var process = new Process();
            process.StartInfo.FileName = "dotnet";
            // 対象を明示しないと、同一ディレクトリに .sln が複数あるとき migrate が失敗または別のソリューションを対象にする
            process.StartInfo.Arguments = $"sln \"{Path.GetFileName(solutionPath)}\" migrate";
            process.StartInfo.WorkingDirectory = solutionDir;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            // 逐次 ReadToEnd では相手側パイプが埋まるとデッドロックしうるため、非同期読み取り + タイムアウト付き待機にする
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(DotnetSlnMigrateTimeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* 既に終了している場合は無視 */ }
                LogMessage($"dotnet sln migrate が {DotnetSlnMigrateTimeoutMs / 1000} 秒以内に終了しなかったため中断しました。.sln のまま続行します。");
                return;
            }
            var stderr = stderrTask.GetAwaiter().GetResult();
            _ = stdoutTask.GetAwaiter().GetResult();
            if (process.ExitCode == 0)
            {
                LogMessage(".slnx ファイルを dotnet sln migrate で生成しました。");
            }
            else
            {
                LogMessage($"dotnet sln migrate が終了コード {process.ExitCode} で終了しました。{stderr}");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"dotnet sln migrate の実行中にエラーが発生しました: {ex.Message}");
        }
    }

    /// <summary>
    /// .slnxファイルからプロジェクト一覧を取得する
    /// </summary>
    /// <param name="solutionPath">.slnxファイルのパス</param>
    /// <returns>プロジェクト情報のリスト</returns>
    private List<ProjectInfo> GetProjectsFromSlnx(string solutionPath)
    {
        var projects = new List<ProjectInfo>();
        var solutionDir = Path.GetDirectoryName(solutionPath)!;

        try
        {
            var doc = XDocument.Load(solutionPath);
            var projectElements = doc.Root?.Descendants("Project") ?? Enumerable.Empty<XElement>();

            foreach (var element in projectElements)
            {
                var relativePath = element.Attribute("Path")?.Value;
                if (string.IsNullOrEmpty(relativePath))
                {
                    continue;
                }

                var absolutePath = Path.GetFullPath(Path.Combine(solutionDir, relativePath));
                var projectName = Path.GetFileNameWithoutExtension(absolutePath);

                projects.Add(new ProjectInfo
                {
                    ProjectName = projectName,
                    AbsolutePath = absolutePath
                });
            }
        }
        catch (Exception ex)
        {
            LogMessage($".slnxファイルの解析中にエラーが発生しました: {ex.Message}");
            throw;
        }

        return projects;
    }

    /// <summary>
    /// .slnファイルからプロジェクト一覧を取得する
    /// </summary>
    /// <param name="solutionPath">.slnファイルのパス</param>
    /// <returns>プロジェクト情報のリスト</returns>
    private List<ProjectInfo> GetProjectsFromSln(string solutionPath)
    {
        var projects = new List<ProjectInfo>();
        try
        {
            var solution = SolutionFile.Parse(solutionPath);
            foreach (var p in solution.ProjectsInOrder)
            {
                if (p.ProjectType.ToString() == "KnownToBeMSBuildProject")
                {
                    projects.Add(new ProjectInfo
                    {
                        ProjectName = p.ProjectName,
                        AbsolutePath = p.AbsolutePath
                    });
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($".slnファイルの解析中にエラーが発生しました: {ex.Message}");
            throw;
        }
        return projects;
    }

    /// <summary>
    /// プロジェクト情報を保持する内部クラス
    /// </summary>
    private sealed class ProjectInfo
    {
        /// <summary>プロジェクト名</summary>
        public string ProjectName { get; init; } = string.Empty;
        /// <summary>プロジェクトファイルの絶対パス</summary>
        public string AbsolutePath { get; init; } = string.Empty;
    }

    /// <summary>
    /// launch.json の1件分の構成オブジェクトを作成する
    /// </summary>
    /// <param name="projectInfo">対象プロジェクトの情報</param>
    /// <param name="solutionDir">ソリューションディレクトリのパス</param>
    /// <param name="solutionName">ソリューション名</param>
    /// <returns>1件分の構成オブジェクト。失敗時は null</returns>
    private object? CreateLaunchConfigEntry(ProjectInfo projectInfo, string solutionDir, string solutionName)
    {
        var projectPath = projectInfo.AbsolutePath;
        var projectDir = Path.GetDirectoryName(projectPath)!;

        // ソリューションディレクトリからの相対パスを取得
        var relativeProjectDir = Path.GetRelativePath(solutionDir, projectDir);
        var projectName = projectInfo.ProjectName;

        try
        {
            // プロジェクトファイルから情報を取得
            var doc = XDocument.Load(projectPath);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            // MSBuild 評価が通れば、Directory.Build.props 等で定義された値も反映される
            var evaluated = EvaluateProject(projectPath);

            // ターゲットフレームワークを取得（単一または複数から先頭のものを選択）
            var targetFramework = evaluated?.TargetFramework;
            if (string.IsNullOrEmpty(targetFramework))
            {
                targetFramework = doc.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value
                               ?? doc.Descendants(ns + "TargetFrameworks").FirstOrDefault()?.Value?.Split(';').FirstOrDefault();
            }

            if (string.IsNullOrEmpty(targetFramework))
            {
                // ターゲットフレームワークが取得できない場合は空文字に設定（.NET Frameworkの古い形式など）
                targetFramework = string.Empty;
            }

            // 出力ファイル名は AssemblyName が指定されていればそれを使う（未指定ならプロジェクト名）
            var assemblyName = evaluated?.AssemblyName;
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                assemblyName = doc.Descendants(ns + "AssemblyName").FirstOrDefault()?.Value;
            }
            var outputName = string.IsNullOrWhiteSpace(assemblyName) ? projectName : assemblyName.Trim();

            string extension;
            string type;

            // VSCode用のパス区切り文字（スラッシュ）に変換し、末尾にスラッシュを付与
            var normalizedRelativePath = relativeProjectDir == "." ? "" : relativeProjectDir.Replace('\\', '/') + "/";

            // .NET (Core) か .NET Framework かを判定して拡張子とデバッガタイプを設定
            if (targetFramework.StartsWith("net") && !targetFramework.Contains("-windows") && !targetFramework.StartsWith("net4"))
            {
                // .NET Core / .NET 5+ (Linux/Mac/Windows 共通)
                extension = ".dll";
                type = "coreclr";
            }
            else if (targetFramework.StartsWith("net4") || string.IsNullOrEmpty(targetFramework))
            {
                // .NET Framework (Windows 専用)
                extension = ".exe";
                type = "clr";
            }
            else
            {
                // その他 (.NET 5+ windows-specific など)
                extension = ".exe";
                type = "coreclr";
            }

            // 出力ディレクトリは MSBuild 評価の TargetDir を最優先で使う。
            // TFM フォルダ・RuntimeIdentifier・カスタム OutputPath・Append* フラグがすべて反映済みで、
            // これらを文字列から組み立て直すと net4x の既定 RID などで誤ったパスになる。
            var outputDir = BuildOutputDirectory(evaluated?.TargetDir, solutionDir, normalizedRelativePath, targetFramework);
            var programPath = $"${{workspaceFolder}}/{outputDir}{outputName}{extension}";

            return new
            {
                name = $".NET Launch ({projectName})",
                type,
                request = "launch",
                preLaunchTask = $"ビルド - {solutionName} ソリューション - Debug",
                program = programPath,
                args = Array.Empty<string>(),
                cwd = $"${{workspaceFolder}}/{normalizedRelativePath.TrimEnd('/')}",
                console = "internalConsole",
                stopAtEntry = false
            };
        }
        catch (Exception ex)
        {
            // 設定作成に失敗した場合はnullを返す（呼び出し側でスキップ件数を警告に載せる）
            LogMessage($"launch 構成の作成に失敗しました: {projectName} - {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// launch.json の program に使う出力ディレクトリ（workspaceFolder からの相対、末尾スラッシュ付き）を組み立てる
    /// </summary>
    /// <param name="targetDir">MSBuild 評価済みの TargetDir（絶対パス）。取得できていない場合は null または空</param>
    /// <param name="solutionDir">ソリューションディレクトリのパス</param>
    /// <param name="normalizedRelativeProjectDir">ソリューションからプロジェクトディレクトリへの相対パス（末尾スラッシュ付き）</param>
    /// <param name="targetFramework">ターゲットフレームワーク</param>
    /// <returns>末尾スラッシュ付きの相対ディレクトリ（ソリューション直下なら空文字）</returns>
    private static string BuildOutputDirectory(string? targetDir, string solutionDir, string normalizedRelativeProjectDir, string targetFramework)
    {
        if (!string.IsNullOrEmpty(targetDir))
        {
            var relative = Path.GetRelativePath(solutionDir, targetDir).Replace('\\', '/').TrimEnd('/');
            return relative is "" or "." ? string.Empty : relative + "/";
        }

        // フォールバック: MSBuild 評価が失敗したときだけ既定レイアウトを仮定して組み立てる。
        // TargetFramework が取れているなら SDK 形式なので、net4x でも TFM フォルダ配下に出力される。
        // TargetFramework が空の場合だけ、TargetFrameworkVersion しか持たない旧形式とみなして bin/Debug 直下にする。
        return string.IsNullOrEmpty(targetFramework)
            ? $"{normalizedRelativeProjectDir}bin/Debug/"
            : $"{normalizedRelativeProjectDir}bin/Debug/{targetFramework}/";
    }

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
    /// VSCode設定ファイルを生成する（同期版。既存 .vscode がある場合は削除せず tasks.json のみ上書きし、既存 launch.json は変更しない）
    /// </summary>
    /// <param name="solutionPath">元のソリューションファイルのパス</param>
    /// <returns>生成結果</returns>
    public VSCodeGenerationResult GenerateVSCodeFiles(string solutionPath)
    {
        return GenerateVSCodeFilesAsync(solutionPath, null).GetAwaiter().GetResult();
    }

    /// <summary>
    /// MSBuild 実行ファイルが利用可能かを検証する。未検出のまま tasks.json を書くと command が空になり、
    /// VS Code 上でビルドタスクが必ず失敗するため、生成前に失敗として扱う。
    /// </summary>
    /// <exception cref="InvalidOperationException">MSBuild.exe が見つからない場合</exception>
    private void EnsureMSBuildAvailable()
    {
        if (!string.IsNullOrWhiteSpace(_msbuildExecutablePath) && File.Exists(_msbuildExecutablePath))
        {
            return;
        }

        LogMessage("MSBuild.exe が見つからないため、VSCode 設定ファイルの生成を中止します。");
        throw new InvalidOperationException(App.Text("Result.Error.MSBuildNotFound"));
    }

    /// <summary>
    /// VSCode設定ファイルを生成する
    /// </summary>
    /// <param name="solutionPath">元のソリューションファイルのパス</param>
    /// <param name="confirmOverwriteVscodeAsync">既存 .vscode がある場合の確認（true=削除して再生成、false=保持して tasks.json のみ上書き）。null の場合は削除しない</param>
    /// <returns>生成結果（警告を含む）</returns>
    public async Task<VSCodeGenerationResult> GenerateVSCodeFilesAsync(string solutionPath, Func<string, Task<bool>>? confirmOverwriteVscodeAsync = null)
    {
        EnsureMSBuildAvailable();
        solutionPath = Path.GetFullPath(solutionPath);
        var vscodeDir = Path.Combine(Path.GetDirectoryName(solutionPath)!, VSCodeDirectoryName);
        // ローカライズと確認ダイアログは呼び出し元の UI スレッドで処理する。
        var messages = GenerationMessages.Capture();
        var replaceDirectory = Directory.Exists(vscodeDir) && confirmOverwriteVscodeAsync != null
            && await confirmOverwriteVscodeAsync(App.Text("Confirm.ExistingVSCode"));
        return await Task.Run(() => GenerateVSCodeFilesCore(solutionPath, vscodeDir, replaceDirectory, messages))
            .ConfigureAwait(false);
    }

    private VSCodeGenerationResult GenerateVSCodeFilesCore(string solutionPath, string vscodeDir,
        bool replaceDirectory, GenerationMessages messages)
    {
        _evaluatedProjectCache.Clear();
        var result = new VSCodeGenerationResult();
        var solutionName = Path.GetFileNameWithoutExtension(solutionPath);
        var pathForOutput = GetSolutionPathForOutput(solutionPath);
        var keepLaunch = !replaceDirectory && File.Exists(Path.Combine(vscodeDir, LaunchJsonFileName));
        var stagingDir = vscodeDir + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Directory.CreateDirectory(stagingDir);
        try
        {
            // 解析・シリアライズ・書き込みが全て成功するまで既存設定に触れない。
            GenerateTasksJson(stagingDir, solutionName, Path.GetFileName(pathForOutput));
            result.TasksJsonWritten = true;
            if (keepLaunch)
                result.LaunchJsonKept = true;
            else
                GenerateLaunchJsonCore(stagingDir, pathForOutput, solutionName, result, messages, true);

            if (replaceDirectory || !Directory.Exists(vscodeDir))
                CommitDirectory(stagingDir, vscodeDir, result, messages);
            else
            {
                // 保持を選んだ場合は生成対象だけを置換し、その他の設定には触れない。
                foreach (var file in Directory.EnumerateFiles(stagingDir))
                    File.Move(file, Path.Combine(vscodeDir, Path.GetFileName(file)), overwrite: true);
            }
            LogMessage("VSCode設定ファイル生成が完了しました。");
            return result;
        }
        finally
        {
            if (Directory.Exists(stagingDir))
                Directory.Delete(stagingDir, true);
        }
    }

    private void CommitDirectory(string stagingDir, string vscodeDir, VSCodeGenerationResult result, GenerationMessages messages)
    {
        if (!Directory.Exists(vscodeDir))
        {
            Directory.Move(stagingDir, vscodeDir);
            return;
        }

        // コピーを作らず、コミット中だけ旧ディレクトリを移動して失敗時に戻す。
        var previousDir = stagingDir + ".previous";
        Directory.Move(vscodeDir, previousDir);
        try
        {
            Directory.Move(stagingDir, vscodeDir);
        }
        catch
        {
            Directory.Move(previousDir, vscodeDir);
            throw;
        }

        try
        {
            Directory.Delete(previousDir, true);
        }
        catch (Exception ex)
        {
            // 新設定は反映済み。後始末の失敗を成功として隠さず、残存パスを通知する。
            LogMessage($"旧設定の後始末に失敗しました: {previousDir}: {ex.Message}");
            result.Warnings.Add(string.Format(messages.CleanupError, previousDir + ": " + ex.Message));
        }
    }

    private sealed record GenerationMessages(string NoExecutableProject, string LaunchFailed,
        string LaunchPartial, string LaunchError, string CleanupError)
    {
        public static GenerationMessages Capture() => new(
            App.Text("Result.Warning.NoExecutableProject"), App.Text("Result.Warning.LaunchFailed"),
            App.Text("Result.Warning.LaunchPartial"), App.Text("Result.Warning.LaunchError"),
            App.Text("Result.Error.Generic"));
    }

    /// <summary>
    /// JSONファイルを保存する
    /// </summary>
    /// <param name="filePath">保存するファイルのパス</param>
    /// <param name="obj">保存するオブジェクト</param>
    private static void SaveJsonFile(string filePath, object obj)
    {
        var jsonString = JsonSerializer.Serialize(obj, JsonOptions);
        var temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, jsonString, System.Text.Encoding.UTF8);
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}

/// <summary>
/// VSCode 設定ファイル生成の結果。完全成功と部分成功を呼び出し側で区別するために使う。
/// </summary>
public sealed class VSCodeGenerationResult
{
    /// <summary>tasks.json を書き出したか</summary>
    public bool TasksJsonWritten { get; set; }

    /// <summary>launch.json を書き出したか</summary>
    public bool LaunchJsonWritten { get; set; }

    /// <summary>既存の launch.json を保持して上書きしなかったか</summary>
    public bool LaunchJsonKept { get; set; }

    /// <summary>launch.json に出力したデバッグ構成の数</summary>
    public int LaunchConfigurationCount { get; set; }

    /// <summary>利用者へ提示すべき警告（ローカライズ済み）</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>警告なしで完了したか</summary>
    public bool IsFullSuccess => Warnings.Count == 0;
}
