using Slx.Client.Core;
using Xunit;

namespace Slx.Client.Tests;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("0.1.0-alpha.9", "0.1.0-alpha.10")]
    [InlineData("0.1.0-alpha.10", "0.1.0-alpha.11")]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    [InlineData("1.0.0", "1.0.1-alpha.1")]
    [InlineData("99999999999999999999.0.0", "100000000000000000000.0.0")]
    public void UsesSemverPrecedenceWithoutLexicalAlphaOrNumericOverflow(string older, string newer)
    {
        Assert.True(ReleaseVersion.TryParse(older, out var first));
        Assert.True(ReleaseVersion.TryParse(newer, out var second));
        Assert.True(first.CompareTo(second) < 0); Assert.True(second.CompareTo(first) > 0);
    }
    [Theory]
    [InlineData("1.0.0+build.1", "1.0.0+build.2")]
    [InlineData("0.1.0-alpha.11+abc", "0.1.0-alpha.11")]
    public void BuildMetadataNeverCreatesAnUpgrade(string one, string two)
    {
        Assert.True(ReleaseVersion.TryParse(one, out var first));
        Assert.True(ReleaseVersion.TryParse(two, out var second));
        Assert.Equal(0, first.CompareTo(second));
    }
    [Theory]
    [InlineData("")]
    [InlineData("v0.1.0-alpha.11")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0-alpha.01")]
    [InlineData("1.0.0-alpha..1")]
    [InlineData("1.0.0/../../evil")]
    [InlineData("1.0.0-alpha.11\n")]
    public void RejectsInvalidAndPathLikeVersions(string input) => Assert.False(ReleaseVersion.TryParse(input, out _));
}
