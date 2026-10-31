using System.Reflection;
using Avalonia.Platform.Storage;
using VStoVSC.Util;
using Xunit;

namespace VStoVSC.Tests;

public sealed class FilePickerTests
{
    // @adversarial @category type @severity medium: 非ローカル URI はキャンセルと区別する。
    [Fact]
    public async Task NonLocalSelectionReportsFailure()
    {
        var file = Stub<IStorageFile>.Create(method => method.Name == "get_Path"
            ? new Uri("https://example.invalid/Example.slnx") : null);
        var service = CreateService([file]);
        var error = await Assert.ThrowsAsync<IOException>(() => service.PickSolutionFileAsync());
        Assert.Contains("LocalPathUnavailable", error.Message);
    }

    private static FilePickerService CreateService(IReadOnlyList<IStorageFile> files)
        => new(() => Stub<IStorageProvider>.Create(method => method.Name == "OpenFilePickerAsync"
            ? Task.FromResult(files) : throw new NotSupportedException(method.Name)));

    public class Stub<T> : DispatchProxy where T : class
    {
        private Func<MethodInfo, object?> _invoke = null!;
        public static T Create(Func<MethodInfo, object?> invoke)
        {
            var proxy = Create<T, Stub<T>>();
            ((Stub<T>)(object)proxy)._invoke = invoke;
            return proxy;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => _invoke(targetMethod!);
    }
}
