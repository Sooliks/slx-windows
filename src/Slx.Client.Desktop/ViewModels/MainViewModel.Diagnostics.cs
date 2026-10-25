using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Slx.Client.Core;
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
            text.AppendLine("Service read UTC: " + (serviceReadAt?.ToString("O") ?? "none"));
            text.AppendLine("UI operation: " + (pendingControlAction is null ? "none" : pendingControlAction + ", pending"));
            if (controlOperationStartedAt is {} started) text.AppendLine($"UI operation started UTC: {started:O}");
            text.AppendLine("Phase: " + (status?.Phase.ToString() ?? "unknown"));
            text.AppendLine("Protection: " + (status?.ProtectionActive.ToString() ?? "unknown"));
            text.AppendLine("Probe passed: " + (status?.ProbePassed.ToString() ?? "unknown"));
            text.AppendLine("Code: " + (LastServiceCode ?? "none"));
            text.AppendLine("Observed UTC: " + (status?.ObservedAt.ToString("O") ?? "unknown"));
            text.AppendLine("Observed meaning: service state publication; DNS and health observations have separate timestamps");
            if (status?.ConnectedAt is {} connected) text.AppendLine($"Connected UTC: {connected:O}");
            if (status?.Recovery is {} recovery)
                text.AppendLine($"Recovery: {recovery.Count}, {recovery.Cause}, {recovery.At:O}");
            if (status?.HealthCheck is {} health)
            {
                text.AppendLine($"Health incidents: {health.Incidents}, retained: {health.RetainedConnections}, at: {health.At:O}");
                AppendHealthAttempt(text, "primary", health.Primary);
                if (health.Repeat is {} repeat) AppendHealthAttempt(text, "repeat", repeat);
                if (health.Alternate is {} alternate) AppendHealthAttempt(text, "alternate", alternate);
            }
            if (status?.DnsCheck is {} dns)
            {
                text.AppendLine($"DNS check UTC: {dns.At:O}");
                AppendDnsAttempt(text, 1, dns.First);
                if (dns.Second is {} second) AppendDnsAttempt(text, 2, second);
                if (dns.Third is {} third) AppendDnsAttempt(text, 3, third);
            }
            TrafficRouteDiagnostics.Append(text, status?.TrafficRoutes);
            return text.ToString();
        }
    }
    private static void AppendHealthAttempt(StringBuilder text, string phase, ProbeObservation observation)
    {
        text.Append($"Health {phase}: {observation.Target}, {observation.Outcome}, {observation.ElapsedMs} ms");
        if (observation.HttpStatus is {} code) text.Append($", http_status={code}");
        text.AppendLine();
    }
    private static void AppendDnsAttempt(StringBuilder text, int number, DnsProbeObservation observation)
    {
        text.Append($"DNS attempt {number}: {observation.Target}, {observation.Outcome}, {observation.ElapsedMs} ms");
        if (observation.ResponseCode is {} response) text.Append($", rcode={response}");
        if (observation.SocketError is {} socket) text.Append($", socket={socket}");
        if (observation.Route is {} route)
        {
            text.Append($", route={route.Status}");
            if (route.NativeError is {} native) text.Append($", route_error={native}");
            if (route.PrefixLength is {} prefix) text.Append($", prefix_length={prefix}");
            if (route.RouteMetric is {} metric) text.Append($", route_metric={metric}");
        }
        text.AppendLine();
    }
}
