using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
/// <summary>Owned local fixtures only; the single launched helper is a hidden copy of Windows cmd.exe with AutoRun disabled.</summary>
public sealed class ExactApplicationNativeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "slx-exact-native-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public void UppercaseManualExecutableBecomesItsRealCaseForXrayAndTheSameWfpIdentity()
    {
        string executable = Create(); string upper = executable.ToUpperInvariant();
        var original = CoreFixtures.Plan() with { Applications = [new(Guid.NewGuid(), "Fixture", upper)] };
        var prepared = ExactApplicationPlan.Prepare(original);
        var selected = Assert.Single(prepared.Applications);
        Assert.Equal("Exact App.EXE", Path.GetFileName(selected.ExecutablePath));
        Assert.Equal(executable, selected.ExecutablePath, ignoreCase: true);
        Assert.NotEqual(upper, selected.ExecutablePath);
        Assert.Equal(TrafficApplicationIds.Read(upper, true), TrafficApplicationIds.Read(selected.ExecutablePath, true));
        Assert.Equal(original.Applications[0].Id, selected.Id); Assert.True(selected.Enabled);
        Assert.Equal(XrayRouteRevision.For(original), XrayRouteRevision.For(prepared));
        Assert.Same(prepared, ExactApplicationPlan.Prepare(prepared));
        var saved = new ProtectedTunnelIntent("fixture-owner", original, "vpn.example.com");
        var restored = ExactApplicationPlan.Prepare(saved);
        Assert.Equal(saved.OwnerSid, restored.OwnerSid); Assert.Equal(saved.EndpointHost, restored.EndpointHost);
        Assert.Equal(selected.ExecutablePath, Assert.Single(restored.Plan.Applications).ExecutablePath);
        Assert.Same(restored, ExactApplicationPlan.Prepare(restored));
    }
    [Fact]
    public void RemovedFileFailsBeforeAnyPreparedPlanIsReturned()
    {
        string executable = Create(); File.Delete(executable);
        var plan = CoreFixtures.Plan() with { Applications = [new(Guid.NewGuid(), "Missing", executable)] };
        Assert.Equal("TRAFFIC_APPLICATION_MISSING", Assert.Throws<ClientFault>(() => ExactApplicationPlan.Prepare(plan)).Code);
    }
    [Fact]
    public void UppercaseOwnedHelperLaunchReportsTheSameImagePathUsedByThePreparedRule()
    {
        Directory.CreateDirectory(root);
        string executable = Path.Combine(root, "Owned Case Helper.exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), executable);
        var start = new System.Diagnostics.ProcessStartInfo(executable.ToUpperInvariant())
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = root
        };
        start.ArgumentList.Add("/D"); start.ArgumentList.Add("/Q"); start.ArgumentList.Add("/K");
        using var child = System.Diagnostics.Process.Start(start)!;
        try
        {
            var image = new StringBuilder(32768); uint size = (uint)image.Capacity;
            Assert.True(QueryFullProcessImageNameW(child.Handle, 0, image, ref size));
            var plan = CoreFixtures.Plan() with { Applications = [new(Guid.NewGuid(), "Owned helper", executable.ToUpperInvariant())] };
            var prepared = ExactApplicationPlan.Prepare(plan);
            Assert.Equal(image.ToString().Replace('\\', '/'), Assert.Single(prepared.Applications).ExecutablePath.Replace('\\', '/'));
            Assert.NotEqual(executable.ToUpperInvariant(), image.ToString());
        }
        finally
        {
            if (!child.HasExited)
            {
                child.StandardInput.WriteLine("exit");
                if (!child.WaitForExit(5000)) { child.Kill(); child.WaitForExit(); }
            }
        }
    }
    [Fact]
    public void LocalJunctionIsRejectedBeforeItsDescendantsAreOpened()
    {
        string executable = Create(); string link = Path.Combine(root, "Fixture Junction");
        using var junction = LocalJunctionFixture.Create(link, Path.GetDirectoryName(executable)!);
        var path = Path.Combine(link, Path.GetFileName(executable));
        var plan = CoreFixtures.Plan() with { Applications = [new(Guid.NewGuid(), "Alias", path)] };
        Assert.Equal(ExactExecutablePath.Unsupported, Assert.Throws<ClientFault>(() => ExactApplicationPlan.Prepare(plan)).Code);
        Assert.True(File.Exists(executable));
    }
    [Fact]
    public void AvailableShortNameDoesNotSilentlyBroadenTheSelectedIdentity()
    {
        string executable = Create(); var buffer = new StringBuilder(32768);
        uint count = GetShortPathNameW(executable, buffer, (uint)buffer.Capacity);
        if (count == 0 || count >= buffer.Capacity || executable.Equals(buffer.ToString(), StringComparison.OrdinalIgnoreCase)) return;
        var plan = CoreFixtures.Plan() with { Applications = [new(Guid.NewGuid(), "Short name", buffer.ToString())] };
        Assert.Equal(ExactExecutablePath.Unsupported, Assert.Throws<ClientFault>(() => ExactApplicationPlan.Prepare(plan)).Code);
    }
    [Theory]
    [InlineData("UnicodeString", 16)]
    [InlineData("ObjectAttributes", 48)]
    [InlineData("IoStatusBlock", 16)]
    public void DocumentedNativeStructuresHaveTheWindowsX64Layout(string name, int bytes) =>
        Assert.Equal(bytes, Marshal.SizeOf(typeof(ExactExecutablePath).GetNestedType(name, BindingFlags.NonPublic)!));
    private string Create()
    {
        var directory = Path.Combine(root, "Mixed Case Directory"); Directory.CreateDirectory(directory);
        var executable = Path.Combine(directory, "Exact App.EXE");
        File.Copy(Environment.ProcessPath!, executable); return executable;
    }
    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathNameW(string path, StringBuilder result, uint size);
    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(nint process, uint flags, StringBuilder result, ref uint size);
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
