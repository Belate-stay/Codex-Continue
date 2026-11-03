using System;
using System.Threading;

namespace CodexContinue;

sealed class SingleInstance : IDisposable
{
    readonly Mutex _mutex;
    readonly EventWaitHandle _activate;
    readonly RegisteredWaitHandle? _listener;
    bool _ownsMutex;

    public bool IsPrimary => _ownsMutex;

    public SingleInstance(string name, Action activate)
    {
        // Open the event first so another launch cannot signal before the owner has a handle.
        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
        _mutex = new Mutex(false, name + ".Mutex");
        try { _ownsMutex = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { _ownsMutex = true; }
        if (_ownsMutex)
            _listener = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) => activate(),
                null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void RequestActivation() => _activate.Set();

    public void Dispose()
    {
        _listener?.Unregister(null);
        if (_ownsMutex) { _mutex.ReleaseMutex(); _ownsMutex = false; }
        _mutex.Dispose();
        _activate.Dispose();
    }
}
