using Microsoft.Build.Construction;
using Microsoft.Build.Locator;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace VS_to_VSC.Services;

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
    public void GenerateLaunchJson(string vscodeDir, string solutionPath, string solutionName)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath)!;
        try
        {
            var projects = GetProjects(solutionPath);
            var executableProjects = projects.Where(p => IsExecutableProject(p.AbsolutePath)).ToList();
            if (executableProjects.Count == 0)
            {
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
                return;
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
            LogMessage(configurations.Count >= 2
                ? $"launch.json生成完了: {configurations.Count}個の構成 + マルチスタートアップ (すべて起動)"
                : $"launch.json生成完了: {executableProjects[0].ProjectName}");
        }
        catch (Exception ex)
        {
            LogMessage($"launch.json生成中にエラーが発生: {ex.Message}");
        }
    }

    /// <summary>
    /// プロジェクトが実行可能かどうか（OutputTypeがExeまたはWinExeか）を判定する
    /// </summary>
    /// <param name="projectPath">プロジェクトファイルのパス</param>
    /// <returns>実行可能な場合はtrue</returns>
    private bool IsExecutableProject(string projectPath)
    {
        try
        {
            if (!File.Exists(projectPath))
            {
                return false;
            }

            // プロジェクトファイルをXMLとして読み込み
            var doc = XDocument.Load(projectPath);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            // OutputType要素を検索
            var outputType = doc.Descendants(ns + "OutputType").FirstOrDefault()?.Value;

            // Exe または WinExe なら実行可能とみなす
            return outputType is "Exe" or "WinExe";
        }
        catch
        {
            // 解析に失敗した場合は実行不可とみなす
            return false;
        }
    }

    /// <summary>
    /// ソリューションファイルからプロジェクト一覧を取得する
    /// </summary>
    /// <param name="solutionPath">ソリューションファイルのパス</param>
    /// <returns>プロジェクト情報のリスト</returns>
    private List<ProjectInfo> GetProjects(string solutionPath)
    {
        var extension = Path.GetExtension(solutionPath).ToLowerInvariant();
        if (extension == ".sln")
        {
            var slnxPath = Path.ChangeExtension(solutionPath, ".slnx");
            if (!File.Exists(slnxPath))
            {
                RunDotnetSlnMigrate(solutionPath);
            }
            if (File.Exists(slnxPath))
            {
                return GetProjectsFromSlnx(slnxPath);
            }
        }
        if (extension == ".slnx")
        {
            return GetProjectsFromSlnx(solutionPath);
        }
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
            using var process = new Process();
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = "sln migrate";
            process.StartInfo.WorkingDirectory = solutionDir;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
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

            // ターゲットフレームワークを取得（単一または複数から先頭のものを選択）
            var targetFramework = doc.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value
                               ?? doc.Descendants(ns + "TargetFrameworks").FirstOrDefault()?.Value?.Split(';').FirstOrDefault();

            if (string.IsNullOrEmpty(targetFramework))
            {
                // ターゲットフレームワークが取得できない場合は空文字に設定（.NET Frameworkの古い形式など）
                targetFramework = string.Empty;
            }

            string programPath;
            string type;

            // VSCode用のパス区切り文字（スラッシュ）に変換し、末尾にスラッシュを付与
            var normalizedRelativePath = relativeProjectDir == "." ? "" : relativeProjectDir.Replace('\\', '/') + "/";

            // .NET (Core) か .NET Framework かを判定してパスとデバッガタイプを設定
            if (targetFramework.StartsWith("net") && !targetFramework.Contains("-windows") && !targetFramework.StartsWith("net4"))
            {
                // .NET Core / .NET 5+ (Linux/Mac/Windows 共通)
                programPath = $"${{workspaceFolder}}/{normalizedRelativePath}bin/Debug/{targetFramework}/{projectName}.dll";
                type = "coreclr";
            }
            else if (targetFramework.StartsWith("net4") || string.IsNullOrEmpty(targetFramework))
            {
                // .NET Framework (Windows 専用)
                programPath = $"${{workspaceFolder}}/{normalizedRelativePath}bin/Debug/{projectName}.exe";
                type = "clr";
            }
            else
            {
                // その他 (.NET 5+ windows-specific など)
                programPath = $"${{workspaceFolder}}/{normalizedRelativePath}bin/Debug/{targetFramework}/{projectName}.exe";
                type = "coreclr";
            }

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
        catch
        {
            // 設定作成に失敗した場合はnullを返す
            return null;
        }
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
    /// VSCode設定ファイルを生成する（同期版。既存 .vscode がある場合は削除せず tasks.json のみ上書き）
    /// </summary>
    /// <param name="solutionPath">元のソリューションファイルのパス</param>
    public void GenerateVSCodeFiles(string solutionPath)
    {
        GenerateVSCodeFilesAsync(solutionPath, null).GetAwaiter().GetResult();
    }

    /// <summary>
    /// VSCode設定ファイルを生成する
    /// </summary>
    /// <param name="solutionPath">元のソリューションファイルのパス</param>
    /// <param name="confirmOverwriteVscodeAsync">既存 .vscode がある場合の確認（true=削除して再生成、false=保持して tasks.json のみ上書き）。null の場合は削除しない</param>
    public async Task GenerateVSCodeFilesAsync(string solutionPath, Func<string, Task<bool>>? confirmOverwriteVscodeAsync = null)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath)!;
        var solutionName = Path.GetFileNameWithoutExtension(solutionPath);
        var solutionFileName = Path.GetFileName(solutionPath);
        var vscodeDir = Path.Combine(solutionDir, VSCodeDirectoryName);

        if (Directory.Exists(vscodeDir))
        {
            const string message = "既存の.vscodeフォルダが見つかりました。\n削除して再生成しますか？\n\n「削除しない」を選ぶと tasks.json のみ上書きします。";
            var deleteAndRegenerate = confirmOverwriteVscodeAsync != null && await confirmOverwriteVscodeAsync(message).ConfigureAwait(false);
            if (deleteAndRegenerate)
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

        GenerateTasksJson(vscodeDir, solutionName, solutionFileName);
        GenerateLaunchJson(vscodeDir, solutionPath, solutionName);
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
}
