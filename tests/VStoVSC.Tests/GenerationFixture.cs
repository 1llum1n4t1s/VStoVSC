using System.Reflection;
using VStoVSC.Util;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace VStoVSC.Tests;

internal sealed class GenerationFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "VStoVSC.Tests", Guid.NewGuid().ToString("N"));
    public string Solution => Path.Combine(Root, "Example.slnx");
    public string Output => Path.Combine(Root, ".vscode");
    public VSCodeGenerator Generator { get; }

    public GenerationFixture(Action<string>? log = null)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(Solution, "<Solution />");
        Generator = new VSCodeGenerator(log);
        // 空ソリューションの検証では MSBuild を実行しない。存在確認だけを満たす。
        typeof(VSCodeGenerator).GetField("_msbuildExecutablePath", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(Generator, typeof(object).Assembly.Location);
    }

    public void ExistingFiles()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "tasks.json"), "original tasks");
        File.WriteAllText(Path.Combine(Output, "launch.json"), "original launch");
        File.WriteAllText(Path.Combine(Output, "settings.json"), "original settings");
    }

    public void AssertOriginalFiles()
    {
        Assert.Equal("original tasks", File.ReadAllText(Path.Combine(Output, "tasks.json")));
        Assert.Equal("original launch", File.ReadAllText(Path.Combine(Output, "launch.json")));
        Assert.Equal("original settings", File.ReadAllText(Path.Combine(Output, "settings.json")));
        Assert.DoesNotContain(Directory.GetDirectories(Root), path => Path.GetFileName(path) != ".vscode");
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
