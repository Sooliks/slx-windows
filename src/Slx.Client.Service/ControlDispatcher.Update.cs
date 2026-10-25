using System.Reflection;
using Slx.Client.Core;

namespace Slx.Client.Service;

internal sealed partial class ControlDispatcher
{
    internal UpdateMaintenance Maintenance { get; init; } = UpdateMaintenance.Volatile();
    private static readonly string ServiceVersion = typeof(ControlDispatcher).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";
    internal bool LoadMaintenance() => Maintenance.Load();
    private TunnelStatus ObservedStatus
    {
        get
        {
            var status = engine.Status;
            return status with { ServiceVersion = ServiceVersion, UpdateMaintenance = Maintenance.Observation,
                Code = Maintenance.IsUnconfirmed ? "UPDATE_MAINTENANCE_UNCONFIRMED" : status.Code };
        }
    }
    private static bool IsMaintenance(string action) => action is "prepare-update" or "claim-update" or "release-update";
    private PipeReply DispatchMaintenance(string sid, PipeRequest request, bool administrator)
    {
        try
        {
            Guid id = request.UpdateSessionId!.Value;
            string sessionOwner = request.Action == "claim-update" && administrator
                ? Maintenance.OwnerSid ?? sid : sid;
            sessions.RequireOwner(sessionOwner);
            if (request.Action == "prepare-update") Maintenance.Prepare(sid, id, engine.IsConfirmedOff);
            else if (request.Action == "claim-update") Maintenance.Claim(sid, id, administrator, engine.IsConfirmedOff);
            else Maintenance.Release(sid, id, engine.IsConfirmedOff);
            return new(true, "OK", ObservedStatus);
        }
        catch (ClientFault fault) { return new(false, fault.Code, ObservedStatus); }
    }
    private bool CanReadMaintenance(string sid) => Maintenance.Observation is null || Maintenance.OwnerSid == sid;
}
