using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Slx.Client.Service;

if(args is ["--release-protection"])
{
    new Slx.Client.Windows.TunnelIntentStore().Save(null);
    using var guard=new Slx.Client.Windows.TrafficGuard();guard.Remove();
    return;
}
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "SLXVPN");
builder.Logging.ClearProviders();
builder.Services.AddSingleton<TunnelEngine>();
builder.Services.AddSingleton<IControlTunnelEngine>(services => services.GetRequiredService<TunnelEngine>());
builder.Services.AddSingleton<ISessionGuard, WindowsSessionGuard>();
builder.Services.AddSingleton<ControlDispatcher>();
builder.Services.AddHostedService<ControlWorker>();
await builder.Build().RunAsync();
