namespace Slx.Client.Desktop.Presentation;
public static class ServiceStateCopy
{
    public static string SafeCode(string? code) => code is { Length: > 0 and <= 64 }
        && (code.All(c => c is >= 'A' and <= 'Z' or '_') || IsNativeTrafficCode(code)) ? code : "SERVICE_FAILURE";
    public static bool IsTrafficFault(string? code) => code?.StartsWith("TRAFFIC_", StringComparison.Ordinal) == true;
    // Retain only definite connection/preflight failures, never a transport timeout or unknown outcome.
    public static bool IsDefinitiveConnectionFault(string? code) => code is
        "ENDPOINT_TIMEOUT" or "ENDPOINT_UNREACHABLE" or "ENDPOINT_NOT_PUBLIC" or "OUTBOUND_INTERFACE_UNAVAILABLE"
        or "RUNTIME_INSTALL_LOCATION" or "RUNTIME_MANIFEST_INVALID" or "RUNTIME_HASH_MISMATCH"
        or "RUNTIME_UNSAFE_PATH" or "RUNTIME_UNSAFE_OWNER" or "RUNTIME_UNSAFE_ACL"
        or "CORE_MISSING" or "CORE_FAILED" or "CORE_UNAVAILABLE" or "CORE_EXITED" or "CORE_START_FAILED" or "CORE_STOP_FAILED"
        or "TUN_ADAPTER_CONFLICT" or "TUN_CLEANUP_PENDING" or "TUN_ADDRESS_FAILED" or "ADAPTER_QUERY_FAILED"
        or "TUNNEL_NOT_READY" or "TUN_NOT_READY" or "VPN_PROBE_FAILED" or "VPN_DNS_FAILED" or "VPN_DNS_ROUTE_FAILED"
        or "VPN_TRAFFIC_ROUTE_FAILED";
    private static bool IsNativeTrafficCode(string code)
    {
        var parts = code.Split('_');
        return parts.Length == 4 && parts[0] == "TRAFFIC" && parts[1] == "PROTECTION"
            && parts[2] is "OPEN" or "PROVIDER" or "SUBLAYER" or "BEGIN" or "COMMIT" or "ENUMERATE" or "DELETE" or "FILTERV4" or "FILTERV6" or "APPLICATION" or "ADAPTER"
            && parts[3].Length == 8 && parts[3].All(char.IsAsciiHexDigit);
    }
    public static bool IsControlFault(string code) => code.StartsWith("SERVICE_", StringComparison.Ordinal)
        || code.StartsWith("UPDATE_MAINTENANCE_", StringComparison.Ordinal)
        || code == "UPDATE_SERVICE_VERSION"
        || code.StartsWith("IPC_", StringComparison.Ordinal) || code is "OPERATION_INTERRUPTED" or "CANCELLED" or "CONTROLLER_OWNED";
    public static string DetailKey(string? code) => code switch
    {
        null => "ServiceCheckingHelp",
        "SERVICE_NOT_INSTALLED" => "ServiceHelp",
        "SERVICE_STARTUP_CLEANUP_PENDING" => "ServiceStartupHelp",
        "UPDATE_MAINTENANCE_UNCONFIRMED" => "UpdateMaintenanceUnconfirmed",
        "UPDATE_MAINTENANCE_ACTIVE" => "UpdateMaintenanceWait",
        "UPDATE_SERVICE_VERSION" => "UpdateVersionMismatch",
        "SERVICE_ACCESS_DENIED" or "SERVICE_IDENTITY_FAILED" => "FaultServiceSecurity",
        "IPC_INVALID" or "IPC_VERSION_MISMATCH" or "IPC_REPLAY_MISMATCH" => "ServiceRequestHelp",
        "SERVICE_TIMEOUT" or "OPERATION_INTERRUPTED" or "CANCELLED" => "ServiceUnknownHelp",
        "CONTROLLER_OWNED" => "FaultOwner",
        "OUTBOUND_INTERFACE_UNAVAILABLE" => "FaultOutboundInterface",
        "ENDPOINT_TIMEOUT" or "ENDPOINT_UNREACHABLE" => "FaultEndpointNetwork",
        "ENDPOINT_NOT_PUBLIC" => "FaultEndpointRejected",
        "RUNTIME_INSTALL_LOCATION" or "RUNTIME_MANIFEST_INVALID" or "RUNTIME_HASH_MISMATCH"
            or "RUNTIME_UNSAFE_PATH" or "RUNTIME_UNSAFE_OWNER" or "RUNTIME_UNSAFE_ACL" => "FaultRuntimeValidation",
        "TUN_ADAPTER_CONFLICT" => "FaultOtherVpn",
        "TUN_CLEANUP_PENDING" => "FaultCleanup",
        "VPN_PROBE_FAILED" => "FaultVpnProbe",
        "VPN_DNS_FAILED" => "FaultVpnDns",
        "VPN_DNS_ROUTE_FAILED" => "FaultVpnDnsRoute",
        "VPN_TRAFFIC_ROUTE_FAILED" => "FaultVpnTrafficRoute",
        "TUN_ADDRESS_FAILED" or "ADAPTER_QUERY_FAILED" or "CORE_MISSING" or "CORE_FAILED" or "CORE_UNAVAILABLE"
            or "TUNNEL_NOT_READY" or "CORE_EXITED" or "CORE_START_FAILED" or "TUN_NOT_READY" or "CORE_STOP_FAILED" => "FaultCore",
        "TRAFFIC_APPLICATION_MISSING" => "FaultTrafficMissing",
        "TRAFFIC_APPLICATION_DENIED" => "FaultTrafficDenied",
        "TRAFFIC_APPLICATION_PATH_UNSUPPORTED" => "FaultTrafficPath",
        "TRAFFIC_APPLICATION_IDENTITY_MISMATCH" => "FaultTrafficIdentity",
        _ when IsTrafficFault(code) => "FaultTrafficProtection",
        _ => "ServiceContactHelp"
    };
}
