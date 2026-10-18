namespace Slx.Client.Desktop.Presentation;
public static class ServiceStateCopy
{
    public static string SafeCode(string? code) => code is { Length: > 0 and <= 64 } && code.All(c => c is >= 'A' and <= 'Z' or '_') ? code : "SERVICE_FAILURE";
    public static bool IsControlFault(string code) => code.StartsWith("SERVICE_", StringComparison.Ordinal)
        || code.StartsWith("IPC_", StringComparison.Ordinal) || code is "OPERATION_INTERRUPTED" or "CANCELLED" or "CONTROLLER_OWNED";
    public static string DetailKey(string? code) => code switch
    {
        null => "ServiceCheckingHelp",
        "SERVICE_NOT_INSTALLED" => "ServiceHelp",
        "SERVICE_ACCESS_DENIED" or "SERVICE_IDENTITY_FAILED" => "FaultServiceSecurity",
        "IPC_INVALID" or "IPC_VERSION_MISMATCH" or "IPC_REPLAY_MISMATCH" => "ServiceRequestHelp",
        "SERVICE_TIMEOUT" or "OPERATION_INTERRUPTED" or "CANCELLED" => "ServiceUnknownHelp",
        "CONTROLLER_OWNED" => "FaultOwner",
        _ => "ServiceContactHelp"
    };
}
