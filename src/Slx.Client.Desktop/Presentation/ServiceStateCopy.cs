namespace Slx.Client.Desktop.Presentation;
public static class ServiceStateCopy
{
    public static string SafeCode(string? code) => code is { Length: > 0 and <= 64 }
        && (code.All(c => c is >= 'A' and <= 'Z' or '_') || IsNativeTrafficCode(code)) ? code : "SERVICE_FAILURE";
    public static bool IsTrafficFault(string? code) => code?.StartsWith("TRAFFIC_", StringComparison.Ordinal) == true;
    private static bool IsNativeTrafficCode(string code)
    {
        var parts = code.Split('_');
        return parts.Length == 4 && parts[0] == "TRAFFIC" && parts[1] == "PROTECTION"
            && parts[2] is "OPEN" or "PROVIDER" or "SUBLAYER" or "BEGIN" or "COMMIT" or "ENUMERATE" or "DELETE" or "FILTERV4" or "FILTERV6" or "APPLICATION" or "ADAPTER"
            && parts[3].Length == 8 && parts[3].All(char.IsAsciiHexDigit);
    }
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
        "TRAFFIC_APPLICATION_MISSING" => "FaultTrafficMissing",
        "TRAFFIC_APPLICATION_DENIED" => "FaultTrafficDenied",
        _ when IsTrafficFault(code) => "FaultTrafficProtection",
        _ => "ServiceContactHelp"
    };
}
