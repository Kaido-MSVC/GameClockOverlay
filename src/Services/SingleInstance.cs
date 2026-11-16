namespace GameClockOverlay.AvaloniaApp.Services;

public sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle signal;
    private RegisteredWaitHandle? wait;
    public bool IsFirst { get; }
    public SingleInstance(string suffix="")
    {
        signal=new(false,EventResetMode.AutoReset,@"Local\GameClockOverlay.Avalonia1.Activate"+suffix);
        mutex=new(false,@"Local\GameClockOverlay.Avalonia1.Singleton"+suffix);
        try { IsFirst=mutex.WaitOne(0); } catch(AbandonedMutexException) { IsFirst=true; }
    }
    public void Listen(Action action) => wait=ThreadPool.RegisterWaitForSingleObject(signal,(_,_)=>action(),null,Timeout.Infinite,false);
    public void Activate() => signal.Set();
    public void Dispose(){wait?.Unregister(null);signal.Dispose();if(IsFirst)mutex.ReleaseMutex();mutex.Dispose();}
}
