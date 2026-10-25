using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Windows.Threading;
namespace Slx.Client.Desktop.Presentation;

/// <summary>One UI per Windows user/session; the only cross-process signal means show the existing window.</summary>
internal sealed class UiSingleInstance : IDisposable
{
    private readonly Mutex ownership;
    private readonly EventWaitHandle activation;
    private RegisteredWaitHandle? listener;
    private int disposed;
    private UiSingleInstance(Mutex ownership, EventWaitHandle activation)
    { this.ownership = ownership; this.activation = activation; }

    internal static bool TryAcquire(bool showExisting, out UiSingleInstance? instance, string? isolatedScope = null)
    {
        instance = null;
        var scope = isolatedScope ?? CurrentScope();
        // Create the event first: a manual launch during owner startup leaves a queued show signal
        // that is consumed only after the first window and its dispatcher are ready.
        var activation = new EventWaitHandle(false, EventResetMode.AutoReset, scope + ".show");
        Mutex? ownership = null;
        bool acquired = false;
        try
        {
            ownership = new Mutex(false, scope + ".owner");
            try { acquired = ownership.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            if (acquired)
            {
                instance = new(ownership, activation);
                return true;
            }
            if (showExisting) activation.Set();
            return false;
        }
        finally
        {
            if (!acquired) { ownership?.Dispose(); activation.Dispose(); }
        }
    }
    internal void StartListening(Dispatcher dispatcher, Action showExisting)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (listener is not null) throw new InvalidOperationException("UI activation is already registered");
        listener = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) =>
        {
            if (Volatile.Read(ref disposed) != 0 || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
            _ = dispatcher.InvokeAsync(() =>
            {
                if (Volatile.Read(ref disposed) == 0) showExisting();
            }, DispatcherPriority.Normal);
        }, null, Timeout.Infinite, executeOnlyOnce: false);
    }
    private static string CurrentScope()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User?.Value ?? throw new InvalidOperationException("Windows user identity is unavailable");
        var user = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sid)));
        using var process = Process.GetCurrentProcess();
        return $@"Local\SLX.VPN.UI.{user}.{process.SessionId}";
    }
    // Named mutex ownership is thread-affine. App acquires on Startup and disposes on Exit,
    // both on its dispatcher thread; fixtures must also dispose on the acquiring thread.
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        listener?.Unregister(null);
        listener = null;
        ownership.ReleaseMutex();
        ownership.Dispose();
        activation.Dispose();
    }
}
