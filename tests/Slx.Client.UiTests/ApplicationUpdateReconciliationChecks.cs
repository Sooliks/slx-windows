using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class ApplicationUpdateReconciliationChecks
{
    private static TunnelStatus Connected(string? revision=null)=>new(TunnelPhase.Connected,DateTimeOffset.UtcNow,
        ProfileName:UiFixtures.Primary.Name,ConnectedAt:DateTimeOffset.UtcNow.AddMinutes(-10),Mode:RoutingMode.SelectedApplications,
        ProbePassed:true,ProtectionActive:true,ApplicationRevision:revision);
    private static MainViewModel Model(Func<PipeRequest,Task<PipeReply>> send,RoutingMode mode=RoutingMode.SelectedApplications)
    {
        var model=new MainViewModel(false,UiFixtures.Subscription,UiFixtures.Preferences(mode));
        UiFixtures.AllowSyntheticApplications(model);model.SendOverride=(request,_)=>send(request);
        model.PollAsync().GetAwaiter().GetResult();return model;
    }
    public static void Run()
    {
        CheckMatchingRevision();CheckNewEditCannotBeCleared();CheckNewEditSurvivesOldAcknowledgement();
        CheckLiveRejections();CheckWholeEditCannotReplayUnknownReconnect();CheckModeChange();
    }
    private static void CheckMatchingRevision()
    {
        var requests=new List<PipeRequest>();var observed=Connected();PipeRequest? mutation=null;
        using var model=Model(request=>
        {
            requests.Add(request);
            if(request.Action=="status")return Task.FromResult(new PipeReply(true,"OK",observed));
            mutation=request;return Task.FromException<PipeReply>(new ClientFault("SERVICE_TIMEOUT"));
        });
        requests.Clear();model.Applications[0].Enabled=false;
        AutoRoutingChecks.Settle(()=>mutation!=null&&!model.IsBusy);
        var target=XrayRouteRevision.For(mutation!.Plan!);
        foreach(var revision in new string?[]{null,new('b',64)})
        {
            observed=observed with{ApplicationRevision=revision};model.PollAsync().GetAwaiter().GetResult();
            Assert.True(model.IsConnected);Assert.True(model.RulesPending);
            Assert.Equal(Text.Catalog["RulesUnconfirmed"],model.RoutingUpdateText);
        }
        observed=observed with{ApplicationRevision=target};model.PollAsync().GetAwaiter().GetResult();
        Assert.False(model.RulesPending);Assert.False(model.ShowMessage);
        Assert.Equal("update-applications",Assert.Single(requests,request=>request.Action!="status").Action);
    }
    private static void CheckNewEditCannotBeCleared()
    {
        PipeRequest? mutation=null;var observed=Connected();
        using var model=Model(request=>
        {
            if(request.Action=="status")return Task.FromResult(new PipeReply(true,"OK",observed));
            mutation=request;return Task.FromException<PipeReply>(new ClientFault("SERVICE_TIMEOUT"));
        });
        model.Applications[0].Enabled=false;AutoRoutingChecks.Settle(()=>mutation!=null&&!model.IsBusy);
        observed=observed with{ApplicationRevision=XrayRouteRevision.For(mutation!.Plan!)};
        model.Applications[0].Enabled=true;model.PollAsync().GetAwaiter().GetResult();
        Assert.True(model.RulesPending); // The old target cannot acknowledge the newer user intent.
    }
    private static void CheckNewEditSurvivesOldAcknowledgement()
    {
        var pending=new TaskCompletionSource<PipeReply>();PipeRequest? mutation=null;
        using var model=Model(request=>
        {
            if(request.Action=="status")return Task.FromResult(new PipeReply(true,"OK",Connected()));
            mutation=request;return pending.Task;
        });
        model.Applications[0].Enabled=false;AutoRoutingChecks.Settle(()=>mutation!=null&&model.IsBusy);
        model.Applications[0].Enabled=true;
        pending.SetResult(new(true,"OK",Connected(XrayRouteRevision.For(mutation!.Plan!))));Sta.Pump();
        Assert.False(model.IsBusy);Assert.True(model.RulesPending);
    }
    private static void CheckLiveRejections()
    {
        foreach(var code in new[]{"ROUTING_UPDATE_FAILED","ROUTING_UPDATE_UNCONFIRMED","TRAFFIC_APPLICATION_MISSING","ROUTING_CONNECTION_CHANGED"})
        {
            int mutations=0;var observed=Connected();
            using var model=Model(request=>
            {
                if(request.Action=="status")return Task.FromResult(new PipeReply(true,"OK",observed));
                mutations++;return Task.FromResult(new PipeReply(false,code,observed));
            });
            model.Applications[0].Enabled=false;AutoRoutingChecks.Settle(()=>mutations==1&&!model.IsBusy);
            Assert.True(model.IsConnected);Assert.True(model.RulesPending);
            var key=code switch{"ROUTING_UPDATE_UNCONFIRMED"=>"RulesApplyUnconfirmed","ROUTING_CONNECTION_CHANGED"=>"RulesConnectionChanged",_=>"RulesApplyFailed"};
            Assert.Equal(Text.Catalog[key],model.Message);
            model.PollAsync().GetAwaiter().GetResult();Assert.True(model.RulesPending);Assert.Equal(1,mutations);
        }
    }
    private static void CheckWholeEditCannotReplayUnknownReconnect()
    {
        var requests=new List<PipeRequest>();var observed=Connected() with{Mode=RoutingMode.WholeComputer};
        using var model=Model(request=>
        {
            requests.Add(request);
            return request.Action=="status"?Task.FromResult(new PipeReply(true,"OK",observed))
                :Task.FromException<PipeReply>(new ClientFault("SERVICE_TIMEOUT"));
        },RoutingMode.WholeComputer);
        requests.Clear();model.SelectedProfile=UiFixtures.Primary with{Port=9444};
        AutoRoutingChecks.Settle(()=>requests.Count>0&&!model.IsBusy);
        model.PollAsync().GetAwaiter().GetResult();model.Applications[0].Enabled=false;
        AutoRoutingChecks.WaitQuiet();
        Assert.True(model.RulesPending);Assert.Equal(Text.Catalog["RulesUnconfirmed"],model.RoutingUpdateText);
        Assert.Equal("reconnect",Assert.Single(requests,request=>request.Action!="status").Action);
    }
    private static void CheckModeChange()
    {
        var requests=new List<PipeRequest>();
        using var model=Model(request=>
        {
            requests.Add(request);return Task.FromResult(new PipeReply(true,"OK",Connected() with{Mode=request.Plan?.Mode??RoutingMode.SelectedApplications}));
        });
        requests.Clear();model.IsWholeMode=true;model.Applications[0].Enabled=false;
        AutoRoutingChecks.Settle(()=>requests.Count>0&&!model.IsBusy);
        var sent=Assert.Single(requests);Assert.Equal("reconnect",sent.Action);Assert.Equal(RoutingMode.WholeComputer,sent.Plan!.Mode);
    }
}
