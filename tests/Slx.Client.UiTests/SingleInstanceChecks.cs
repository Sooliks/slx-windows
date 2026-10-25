using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using Slx.Client.Desktop.Presentation;
using Xunit;
namespace Slx.Client.UiTests;
internal static class SingleInstanceChecks
{
    public static void Run()
    {
        ManualDuplicateQueuesOnlyShowAndTrayDuplicateStaysQuiet();
        DisposeReleasesOwnershipAndCancelsQueuedActivation();
        IsolatedScopesDoNotInterfere();
    }
    private static void ManualDuplicateQueuesOnlyShowAndTrayDuplicateStaysQuiet()
    {
        var scope = Scope();
        Assert.True(UiSingleInstance.TryAcquire(true, out var owner, scope));
        using (owner!)
        {
            Assert.False(LaunchElsewhere(scope, false));
            // No listener exists yet, so this checks the event itself without a timing-dependent wait.
            using (var pendingShow = EventWaitHandle.OpenExisting(scope + ".show"))
                Assert.False(pendingShow.WaitOne(0));
            Assert.False(LaunchElsewhere(scope, true));
            var shown = 0;
            var dispatcher = Dispatcher.CurrentDispatcher;
            owner!.StartListening(dispatcher, () =>
            {
                Assert.True(dispatcher.CheckAccess());
                shown++;
            });
            AutoRoutingChecks.Settle(() => shown == 1);
            Assert.False(LaunchElsewhere(scope, true));
            AutoRoutingChecks.Settle(() => shown == 2);
        }
    }
    private static void DisposeReleasesOwnershipAndCancelsQueuedActivation()
    {
        var scope = Scope();
        Assert.True(UiSingleInstance.TryAcquire(true, out var owner, scope));
        var shown = 0;
        owner!.StartListening(Dispatcher.CurrentDispatcher, () => shown++);
        Assert.False(LaunchElsewhere(scope, true));
        // The UI dispatcher has not pumped the show callback. Disposing must prevent it acting later.
        owner.Dispose(); owner.Dispose(); Sta.Pump();
        Assert.Equal(0, shown);
        // A different thread can now own and release the same objects, rather than recursively taking
        // the mutex already owned by this test's dispatcher thread.
        Assert.True(LaunchElsewhere(scope, false));
        Assert.True(UiSingleInstance.TryAcquire(false, out var replacement, scope));
        replacement!.Dispose();
    }
    private static void IsolatedScopesDoNotInterfere()
    {
        var first = Scope(); var second = Scope();
        Assert.True(UiSingleInstance.TryAcquire(true, out var firstOwner, first));
        Assert.True(UiSingleInstance.TryAcquire(true, out var secondOwner, second));
        using (firstOwner!)
        using (secondOwner!)
        {
            Assert.False(LaunchElsewhere(first, true));
            using var firstEvent = EventWaitHandle.OpenExisting(first + ".show");
            using var secondEvent = EventWaitHandle.OpenExisting(second + ".show");
            Assert.True(firstEvent.WaitOne(0));
            Assert.False(secondEvent.WaitOne(0));
        }
    }
    private static string Scope() => $@"Local\SLX.UI.Tests.{Guid.NewGuid():N}";
    private static bool LaunchElsewhere(string scope, bool showExisting)
    {
        bool acquired = false;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                acquired = UiSingleInstance.TryAcquire(showExisting, out var instance, scope);
                instance?.Dispose();
            }
            catch (Exception error) { failure = error; }
        }) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(3)), "Isolated UI-instance admission did not finish");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        return acquired;
    }
}
