namespace Slx.Client.Core;

/// <summary>Owned split defaults capture public data before a competing ordinary default route.</summary>
internal static class XraySystemRoutes
{
    internal static string[] Build() =>
    [
        "0.0.0.0/1", "128.0.0.0/1", "::/1", "8000::/1",
        "1.1.1.1/32", "2606:4700:4700::1111/128"
    ];
}
