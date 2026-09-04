using System.Collections.Concurrent;
using VStoVSC.Util;
using Xunit;

namespace VStoVSC.Tests;

public sealed class GenerationAdversarialTests
{
    // @adversarial @category resource @severity high
    // 再生成承認時でも、ロックによる旧ディレクトリ移動失敗で設定を失わない。
    [Fact]
    public async Task LockedLaunchFilePreventsDirectoryReplacement()
    {
        using var fixture = new GenerationFixture();
        fixture.ExistingFiles();
        Exception? error;
        using (var locked = new FileStream(Path.Combine(fixture.Output, "launch.json"),
            FileMode.Open, FileAccess.Read, FileShare.None))
        {
            error = await Record.ExceptionAsync(() => fixture.Generator.GenerateVSCodeFilesAsync(
                fixture.Solution, _ => Task.FromResult(true)));
        }
        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
        fixture.AssertOriginalFiles();
    }

    // @adversarial @category resource @severity high
    // 反映直前に生成物が失われても、旧ディレクトリへのロールバックが成立する。
    [Fact]
    public async Task MissingStagingDirectoryRollsBackReplacement()
    {
        GenerationFixture? active = null;
        using var fixture = new GenerationFixture(message =>
        {
            if (active != null && message.StartsWith("実行可能プロジェクトが見つからない", StringComparison.Ordinal))
            {
                var staging = Assert.Single(Directory.GetDirectories(active.Root),
                    path => Path.GetFileName(path).EndsWith(".tmp", StringComparison.Ordinal));
                Directory.Delete(staging, recursive: true);
            }
        });
        active = fixture;
        fixture.ExistingFiles();
        await Assert.ThrowsAnyAsync<IOException>(() => fixture.Generator.GenerateVSCodeFilesAsync(
            fixture.Solution, _ => Task.FromResult(true)));
        fixture.AssertOriginalFiles();
    }

    // @adversarial @category state @severity high
    // 解析失敗時は、削除を承認していても既存設定をそのまま残す。
    [Fact]
    public async Task MalformedSolutionPreservesExistingSettings()
    {
        using var fixture = new GenerationFixture();
        fixture.ExistingFiles();
        File.WriteAllText(fixture.Solution, "<Solution>");
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Generator.GenerateVSCodeFilesAsync(
            fixture.Solution, _ => Task.FromResult(true)));
        fixture.AssertOriginalFiles();
    }

    // @adversarial @category resource @severity high
    // 書き込み先がロックされていても、既存の tasks.json を切り詰めない。
    [Fact]
    public async Task LockedTasksFilePreservesExistingSettings()
    {
        using var fixture = new GenerationFixture();
        fixture.ExistingFiles();
        using (var locked = new FileStream(Path.Combine(fixture.Output, "tasks.json"),
            FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var error = await Record.ExceptionAsync(() => fixture.Generator.GenerateVSCodeFilesAsync(
                fixture.Solution, _ => Task.FromResult(false)));
            Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
        }
        fixture.AssertOriginalFiles();
    }

    // @adversarial @category concurrency @severity high
    // UI 相当の専用スレッドで確認し、重い生成処理は別スレッドで実行する。
    [Fact]
    public async Task ConversionWorkDoesNotRunOnCallingThread()
    {
        var workThreads = new ConcurrentQueue<int>();
        using var fixture = new GenerationFixture(message =>
        {
            if (message.Contains("tasks.json", StringComparison.Ordinal))
                workThreads.Enqueue(Environment.CurrentManagedThreadId);
        });
        fixture.ExistingFiles();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var caller = 0;
        var confirmationThread = 0;
        var thread = new Thread(() =>
        {
            try
            {
                caller = Environment.CurrentManagedThreadId;
                fixture.Generator.GenerateVSCodeFilesAsync(fixture.Solution, _ =>
                {
                    confirmationThread = Environment.CurrentManagedThreadId;
                    return Task.FromResult(false);
                }).GetAwaiter().GetResult();
                completion.SetResult();
            }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true };
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(caller, confirmationThread);
        Assert.NotEmpty(workThreads);
        Assert.DoesNotContain(caller, workThreads);
    }

    // @adversarial @category environment @severity medium
    // 不正な入力で migrate を二重起動せず、元の .sln の解析エラーを返す。
    [Fact]
    public async Task FailedMigrationIsAttemptedOnce()
    {
        var logs = new List<string>();
        using var fixture = new GenerationFixture(logs.Add);
        var solution = Path.ChangeExtension(fixture.Solution, ".sln");
        File.Delete(fixture.Solution);
        File.WriteAllText(solution, "invalid solution");
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Generator.GenerateVSCodeFilesAsync(solution));
        Assert.Single(logs, message => message.StartsWith("dotnet sln migrate", StringComparison.Ordinal));
        Assert.False(Directory.Exists(fixture.Output));
    }
}
