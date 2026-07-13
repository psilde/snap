using Snap.Services;
using Xunit;

namespace Snap.Tests;

public class StartupServiceTests
{
    [Fact]
    public void PathsMatch_WhenStoredEqualsCurrent_ReturnsTrue()
    {
        Assert.True(StartupService.PathsMatch(@"C:\Apps\Snap.exe", @"C:\Apps\Snap.exe"));
    }

    [Fact]
    public void PathsMatch_IsCaseInsensitive()
    {
        Assert.True(StartupService.PathsMatch(@"C:\Apps\Snap.exe", @"c:\apps\snap.exe"));
    }

    [Fact]
    public void PathsMatch_WhenStoredIsNull_ReturnsFalse()
    {
        Assert.False(StartupService.PathsMatch(null, @"C:\Apps\Snap.exe"));
    }

    [Fact]
    public void PathsMatch_WhenStoredIsEmpty_ReturnsFalse()
    {
        Assert.False(StartupService.PathsMatch(string.Empty, @"C:\Apps\Snap.exe"));
    }

    [Fact]
    public void PathsMatch_WhenPathsDiffer_ReturnsFalse()
    {
        Assert.False(StartupService.PathsMatch(@"C:\Apps\Other.exe", @"C:\Apps\Snap.exe"));
    }
}
