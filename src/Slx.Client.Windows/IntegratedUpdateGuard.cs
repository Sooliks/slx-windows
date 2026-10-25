using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Slx.Client.Windows;

/// <summary>A show-only signal for one unprivileged updater in this Windows user/session.</summary>
public sealed class IntegratedUpdateGuard : IDisposable
{
    private readonly Mutex ownership;
    private readonly EventWaitHandle activation;
    private RegisteredWaitHandle? listener;
    private int disposed;
    private IntegratedUpdateGuard(Mutex mutex, EventWaitHandle signal) { ownership = mutex; activation = signal; }
    public static bool TryActivateExisting()
    {
        using var ownership = new Mutex(false, Scope() + ".owner"); bool acquired;
        try { acquired = ownership.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
        if (acquired) { ownership.ReleaseMutex(); return false; }
        using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, Scope() + ".show"); signal.Set(); return true;
    }
    public static IntegratedUpdateGuard? TryAcquire()
    {
        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, Scope() + ".show");
        var mutex = new Mutex(false, Scope() + ".owner"); bool acquired;
        try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
        if (acquired) return new(mutex, signal);
        mutex.Dispose(); signal.Set(); signal.Dispose(); return null;
    }
    public void Listen(Action show)
    {
        if (listener is not null) throw new InvalidOperationException("Updater activation already registered");
        listener = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) =>
        { if (Volatile.Read(ref disposed) == 0) show(); }, null, Timeout.Infinite, false);
    }
    private static string Scope()
    {
        using var identity = WindowsIdentity.GetCurrent(); using var process = Process.GetCurrentProcess();
        var user = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.User!.Value)));
        return $@"Local\SLX.VPN.Updater.{user}.{process.SessionId}";
    }
    // Acquire and release on the same dispatcher thread, as required for a named mutex.
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        listener?.Unregister(null); ownership.ReleaseMutex(); ownership.Dispose(); activation.Dispose();
    }
}
