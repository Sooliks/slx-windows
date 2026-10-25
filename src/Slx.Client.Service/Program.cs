using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Slx.Client.Service;

if(UpdateClaimCommand.Requested(args))
{
    Environment.ExitCode=await UpdateClaimCommand.RunAsync(args);
    return;
}
if(args is ["--release-protection"])
{
    var store=new Slx.Client.Windows.TunnelIntentStore();
    store.Save(null);store.SaveMaintenance(null);
    using var guard=new Slx.Client.Windows.TrafficGuard();guard.Remove();
    return;
}
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "SLXVPN");
builder.Logging.ClearProviders();
builder.Services.AddSingleton<TunnelEngine>();
builder.Services.AddSingleton<IControlTunnelEngine>(services => services.GetRequiredService<TunnelEngine>());
builder.Services.AddSingleton<ISessionGuard, WindowsSessionGuard>();
builder.Services.AddSingleton(services => new ControlDispatcher(services.GetRequiredService<IControlTunnelEngine>(),
    services.GetRequiredService<ISessionGuard>()) { Maintenance = new(new Slx.Client.Windows.TunnelIntentStore()) });
builder.Services.AddHostedService<ControlWorker>();
await builder.Build().RunAsync();
