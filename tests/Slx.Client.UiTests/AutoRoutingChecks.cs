using System.Diagnostics;
using Slx.Client.Core;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class AutoRoutingChecks
{
    private static MainViewModel Model(List<PipeRequest> requests, bool unknown=false)
    {
        var model=new MainViewModel(false,UiFixtures.Subscription,UiFixtures.Preferences(RoutingMode.SelectedApplications));
        model.SendOverride=(request,_)=>{
            requests.Add(request);
            if(unknown&&request.Action!="status")return Task.FromException<PipeReply>(new ClientFault("SERVICE_TIMEOUT"));
            return Task.FromResult(new PipeReply(true,"OK",new(
                request.Action=="disconnect"?TunnelPhase.Disconnected:TunnelPhase.Connected,DateTimeOffset.UtcNow,
                Mode:request.Plan?.Mode??RoutingMode.SelectedApplications,ProbePassed:request.Action!="disconnect")));
        };
        model.PollAsync().GetAwaiter().GetResult();requests.Clear();
        return model;
    }
    private static void Settle(Func<bool> done)
    {
        var watch=Stopwatch.StartNew();
        while(!done()&&watch.Elapsed<TimeSpan.FromSeconds(3)){Thread.Sleep(10);Sta.Pump();}
        Assert.True(done());
    }
    public static void Run()
    {
        var requests=new List<PipeRequest>();
        using(var model=Model(requests))
        {
            var app=new AppRule(Guid.NewGuid(),"Auto app",@"C:\Synthetic\Auto.exe");
            model.AddDiscoveredCommand.Execute(app);
            model.Applications[0].Enabled=false;model.Applications[0].Enabled=true;
            Assert.Empty(requests);
            Settle(()=>requests.Count>0&&!model.IsBusy);
            var sent=Assert.Single(requests);
            Assert.Equal("reconnect",sent.Action);
            Assert.Contains(sent.Plan!.Applications,x=>x.Id==app.Id&&x.Enabled);
            Assert.False(model.RulesPending);
        }
        requests.Clear();
        using(var model=Model(requests,true))
        {
            model.AddDiscoveredCommand.Execute(new AppRule(Guid.NewGuid(),"Unknown app",@"C:\Synthetic\Unknown.exe"));
            Settle(()=>requests.Count>0&&!model.IsBusy);
            Assert.Single(requests);Assert.True(model.RulesPending);Assert.False(model.IsConnected);
            var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<600){Thread.Sleep(10);Sta.Pump();}
            Assert.Single(requests);
        }
        requests.Clear();
        using(var model=Model(requests))
        {
            foreach(var app in model.Applications)app.Enabled=false;
            Settle(()=>requests.Count>0&&!model.IsBusy);
            Assert.Equal("disconnect",Assert.Single(requests).Action);Assert.False(model.RulesPending);
        }
        requests.Clear();
        using(var model=Model(requests))
        {
            model.AddDiscoveredCommand.Execute(new AppRule(Guid.NewGuid(),"Cancelled app",@"C:\Synthetic\Cancel.exe"));
            model.ToggleConnectionCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<600){Thread.Sleep(10);Sta.Pump();}
            Assert.Equal("disconnect",Assert.Single(requests).Action);
        }
        requests.Clear();
        using(var model=new MainViewModel(false,UiFixtures.Subscription,UiFixtures.Preferences(RoutingMode.WholeComputer)))
        {
            model.SendOverride=(request,_)=>{
                requests.Add(request);
                return Task.FromResult(new PipeReply(true,"OK",new(TunnelPhase.Connected,DateTimeOffset.UtcNow,
                    ProfileName:request.Plan?.Profile.Name??UiFixtures.Primary.Name,Mode:RoutingMode.WholeComputer,ProbePassed:true)));
            };
            model.PollAsync().GetAwaiter().GetResult();requests.Clear();
            model.AddDiscoveredCommand.Execute(new AppRule(Guid.NewGuid(),"Whole app",@"C:\Synthetic\Whole.exe"));
            Assert.False(model.RulesPending);Assert.Empty(requests);
            model.SelectedProfile=UiFixtures.Alternative;
            Settle(()=>requests.Count>0&&!model.IsBusy);
            Assert.Equal(UiFixtures.Alternative.Id,Assert.Single(requests).Plan!.Profile.Id);
        }

    }
}
