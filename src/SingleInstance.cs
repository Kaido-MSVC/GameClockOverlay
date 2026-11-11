using System;
using System.Threading;

namespace GameClockOverlay;

internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle recovery;
    private RegisteredWaitHandle? wait;
    internal bool IsFirst { get; }
    internal SingleInstance()
    {
        // Publish the signal handle before advertising ownership of the mutex.
        recovery = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\GameClockOverlay.Recover.v0.2");
        try
        {
            mutex = new Mutex(true, @"Local\GameClockOverlay.v0.1", out bool first);
            IsFirst = first;
        }
        catch { recovery.Dispose(); throw; }
    }
    internal void Listen(Action callback) => wait = ThreadPool.RegisterWaitForSingleObject(
        recovery, (_, _) => callback(), null, Timeout.Infinite, false);
    internal bool RequestRecovery() => recovery.Set(); // No wait on the other process / UI thread.
    internal void StopListening() { wait?.Unregister(null); wait = null; }
    public void Dispose()
    {
        StopListening();
        recovery.Dispose();
        if (IsFirst) mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
