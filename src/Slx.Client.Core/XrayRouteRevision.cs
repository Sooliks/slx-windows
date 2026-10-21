using System.Security.Cryptography;
using System.Text.Json;
namespace Slx.Client.Core;
public static class XrayRouteRevision
{
    public static string For(ConnectPlan plan)
    {
        PlanValidator.Validate(plan);
        var paths = plan.Applications.Where(app => plan.Mode == RoutingMode.SelectedApplications && app.Enabled).Select(app => app.ExecutablePath.Replace('\\', '/').ToLowerInvariant())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(new { Schema = 1, plan.Mode, plan.Profile, Paths = paths });
        try { return Convert.ToHexStringLower(SHA256.HashData(canonical)); }
        finally { CryptographicOperations.ZeroMemory(canonical); }
    }
}
