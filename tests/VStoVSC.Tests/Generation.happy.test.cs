using System.Text.Json;
using Xunit;

namespace VStoVSC.Tests;

public sealed class GenerationHappyTests
{
    // @happypath @severity high: 空ソリューションでもビルドタスクを生成する。
    [Fact]
    public async Task EmptySolutionGeneratesTasksAndWarning()
    {
        using var fixture = new GenerationFixture();
        var result = await fixture.Generator.GenerateVSCodeFilesAsync(fixture.Solution);
        Assert.True(result.TasksJsonWritten);
        Assert.False(result.LaunchJsonWritten);
        Assert.Contains("NoExecutableProject", Assert.Single(result.Warnings));
        AssertTasks(fixture);
    }

    // @happypath @severity high: 保持を選んだ場合は利用者の設定を維持する。
    [Fact]
    public async Task DeclinedReplacementPreservesSettingsAndLaunch()
    {
        using var fixture = new GenerationFixture();
        fixture.ExistingFiles();
        var count = 0;
        var result = await fixture.Generator.GenerateVSCodeFilesAsync(fixture.Solution, _ =>
        {
            count++;
            return Task.FromResult(false);
        });
        Assert.Equal(1, count);
        Assert.True(result.LaunchJsonKept);
        Assert.Empty(result.Warnings);
        Assert.Equal("original launch", File.ReadAllText(Path.Combine(fixture.Output, "launch.json")));
        Assert.Equal("original settings", File.ReadAllText(Path.Combine(fixture.Output, "settings.json")));
        AssertTasks(fixture);
    }

    // @happypath @severity high: 再生成承認時は生成完了後に旧フォルダ全体を置換する。
    [Fact]
    public async Task ApprovedReplacementCommitsNewDirectory()
    {
        using var fixture = new GenerationFixture();
        fixture.ExistingFiles();
        var nested = Directory.CreateDirectory(Path.Combine(fixture.Output, "old"));
        File.WriteAllText(Path.Combine(nested.FullName, "extra.json"), "old");
        var result = await fixture.Generator.GenerateVSCodeFilesAsync(fixture.Solution, _ => Task.FromResult(true));
        Assert.True(result.TasksJsonWritten);
        Assert.Contains("NoExecutableProject", Assert.Single(result.Warnings));
        Assert.Single(Directory.GetFileSystemEntries(fixture.Output));
        Assert.Single(Directory.GetDirectories(fixture.Root));
        AssertTasks(fixture);
    }

    private static void AssertTasks(GenerationFixture fixture)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Output, "tasks.json")));
        Assert.Equal("2.0.0", document.RootElement.GetProperty("version").GetString());
        var tasks = document.RootElement.GetProperty("tasks");
        Assert.Equal(4, tasks.GetArrayLength());
        Assert.All(tasks.EnumerateArray(), task => Assert.Equal(typeof(object).Assembly.Location,
            task.GetProperty("command").GetString()));
    }
}
