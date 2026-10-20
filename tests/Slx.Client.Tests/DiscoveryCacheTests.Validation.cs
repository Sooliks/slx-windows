using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed partial class DiscoveryCacheTests
{
    [Theory]
    [InlineData(@"\\server\share\app.exe")]
    [InlineData(@"\\?\C:\apps\app.exe")]
    [InlineData(@"apps\app.exe")]
    [InlineData(@"C:apps\app.exe")]
    [InlineData(@"C:\apps\..\app.exe")]
    [InlineData(@"C:\apps\.\app.exe")]
    [InlineData(@"C:\apps\\app.exe")]
    [InlineData(@"C:\apps \app.exe")]
    [InlineData(@"C:\apps.\app.exe")]
    [InlineData(@"C:\apps\app.exe --silent")]
    [InlineData(@"C:\apps\app.exe --other.exe")]
    [InlineData("\"C:\\apps\\app.exe\"")]
    [InlineData(@"C:\apps\app.exe:stream.exe")]
    [InlineData(@"C:\apps\*.exe")]
    [InlineData(@"C:\apps\NUL.exe")]
    [InlineData(@"C:\apps\COM1\app.exe")]
    [InlineData(@"C:/apps/app.exe")]
    [InlineData("C:\\apps\\app\n.exe")]
    public void UnsafeCachePathsAreRejectedBeforeAnyFileIsCreated(string path)
    {
        Fault(() => Store().SaveDiscoveryCache(Snapshot(App(path: path))));
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" App")]
    [InlineData("App\nName")]
    [InlineData("App\u202eName")]
    public void InvalidApplicationNamesCannotEnterTheCache(string name) =>
        Fault(() => Store().SaveDiscoveryCache(Snapshot(App(name))));

    [Fact]
    public void NamesCanContainNonLatinTextAndValidUnicodeScalarPairs()
    {
        var app = App("Приложение 🎮");
        Store().SaveDiscoveryCache(Snapshot(app));
        Assert.Equal(app, Assert.Single(Store().LoadDiscoveryCache()!.Applications));
    }

    [Fact]
    public void InvalidCountsIdsDuplicatesAndTimestampsAreRejectedOnSave()
    {
        var app = App();
        var now = DateTimeOffset.UtcNow;
        ApplicationDiscoverySnapshot[] invalid =
        [
            new(now, null!), new(now, [null!]), new(now, [app with { Id = Guid.Empty }]),
            new(now, [app, App(path: @"C:\Other\app.exe") with { Id = app.Id }]),
            new(now, [app, App(path: app.ExecutablePath.ToUpperInvariant())]),
            new(now, Enumerable.Range(0, 301).Select(index => App(path: @"C:\Apps\a" + index + ".exe")).ToArray()),
            new(now, [App(new string('n', 101))]), new(now, [App("App" + new string('\ud800', 1))]),
            new(now.AddDays(1), [app]), new(DateTimeOffset.MinValue, [app]),
            new(now.ToOffset(TimeSpan.FromHours(3)), [app])
        ];
        foreach (var snapshot in invalid) Fault(() => Store().SaveDiscoveryCache(snapshot));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void CorrectDpapiDoesNotBypassEnvelopeSchemaAndRowValidation()
    {
        var now = DateTimeOffset.UtcNow;
        var app = App();
        object[] invalid =
        [
            new { SchemaVersion = 2, CapturedAt = now, Applications = new[] { app } },
            new { SchemaVersion = 1, CapturedAt = now, Applications = (AppRule[]?)null },
            new { SchemaVersion = 1, CapturedAt = now, Applications = new[] { app with { Id = Guid.Empty } } },
            new { SchemaVersion = 1, CapturedAt = now, Applications = new[] { app with { ExecutablePath = @"..\a.exe" } } },
            new { SchemaVersion = 1, CapturedAt = now.AddDays(1), Applications = new[] { app } },
            new { SchemaVersion = 1, CapturedAt = now, Applications = new[] { app, app with { Id = Guid.NewGuid() } } },
            new { SchemaVersion = 1, CapturedAt = now, Applications = "wrong shape" },
            new { SchemaVersion = 1, CapturedAt = now, Applications = Array.Empty<AppRule>(), Padding = new string('x', 1044481) }
        ];
        foreach (var envelope in invalid)
        {
            WriteEnvelope(envelope);
            Fault(() => Store().LoadDiscoveryCache());
        }
    }
}
