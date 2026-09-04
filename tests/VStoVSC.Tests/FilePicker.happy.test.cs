using System.Reflection;
using Avalonia.Platform.Storage;
using VStoVSC.Util;
using Xunit;

namespace VStoVSC.Tests;

public sealed partial class FilePickerTests
{
    // @happypath @severity medium: キャンセル時だけ null を返す。
    [Fact]
    public async Task CancellationReturnsNull()
    {
        var service = CreateService([]);
        Assert.Null(await service.PickSolutionFileAsync());
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
