using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Slx.Client.Desktop.Presentation;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    public RelayCommand CopyDiagnosticsCommand { get; private set; } = null!;
    private void InitializeDiagnostics() => CopyDiagnosticsCommand = new RelayCommand(() =>
    {
        try { Clipboard.SetText(SafeDiagnostics); Notice("DiagnosticsCopied"); }
        catch (ExternalException) { Notice("DiagnosticsFailed"); }
    });
    internal string SafeDiagnostics
    {
        get
        {
            var text = new StringBuilder();
            text.AppendLine(VersionLabel);
            text.AppendLine("Windows: " + Environment.OSVersion.Version);
            text.AppendLine("Captured UTC: " + DateTimeOffset.UtcNow.ToString("O"));
            text.AppendLine("Service observation: " + (statusFresh ? "current" : "unknown/stale"));
            text.AppendLine("Phase: " + (status?.Phase.ToString() ?? "unknown"));
            text.AppendLine("Protection: " + (status?.ProtectionActive.ToString() ?? "unknown"));
            text.AppendLine("Probe passed: " + (status?.ProbePassed.ToString() ?? "unknown"));
            text.AppendLine("Code: " + (LastServiceCode ?? "none"));
            text.AppendLine("Observed UTC: " + (status?.ObservedAt.ToString("O") ?? "unknown"));
            if (status?.Recovery is {} recovery)
                text.AppendLine($"Recovery: {recovery.Count}, {recovery.Cause}, {recovery.At:O}");
            if (status?.HealthCheck is {} health)
                text.AppendLine($"Health incidents: {health.Incidents}, retained: {health.RetainedConnections}, at: {health.At:O}");
            return text.ToString();
        }
    }
}
