using Avalonia.Platform.Storage;
using Xunit;

namespace VStoVSC.Tests;

public sealed partial class FilePickerTests
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
}
