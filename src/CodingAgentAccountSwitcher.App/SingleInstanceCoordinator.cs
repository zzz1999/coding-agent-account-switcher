using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace CodingAgentAccountSwitcher.App;

/// <summary>
/// Coordinates one interactive application instance per Windows user and session.
/// A named mutex is held by the primary instance, while an auto-reset event
/// lets later launches ask the first window to return to the foreground.
/// </summary>
internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _instanceMarker;
    private readonly Thread _ownershipThread;
    private readonly ManualResetEventSlim _releaseOwnership = new();
    private readonly EventWaitHandle _activationSignal;
    private readonly RegisteredWaitHandle? _registeredWait;
    private int _disposed;

    internal SingleInstanceCoordinator(string instanceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceKey);

        var suffix = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(instanceKey)));
        var markerName = $@"Local\CodingAgentAccountSwitcher.Instance.{suffix}";
        var activationSignalName = $@"Local\CodingAgentAccountSwitcher.Activate.{suffix}";

        // Create/open the signal before electing the primary instance. An
        // immediate second launch can then set it even while the first process
        // is still constructing its window; AutoResetEvent retains that signal
        // until the registered wait begins.
        _activationSignal = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            activationSignalName);

        _instanceMarker = new Mutex(initiallyOwned: false, markerName);
        var election = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        // Mutex ownership is thread-affine. A dedicated thread both acquires
        // and releases it, so disposal need not run on the construction thread.
        // An open secondary handle must not keep a stopped primary "alive".
        _ownershipThread = new Thread(() => HoldInstanceOwnership(election))
        {
            IsBackground = true,
            Name = "CAAS instance ownership"
        };
        try
        {
            _ownershipThread.Start();
            IsPrimary = election.Task.GetAwaiter().GetResult();
        }
        catch
        {
            _activationSignal.Dispose();
            _instanceMarker.Dispose();
            _releaseOwnership.Dispose();
            throw;
        }
        if (!IsPrimary)
        {
            return;
        }

        try
        {
            _registeredWait = ThreadPool.RegisterWaitForSingleObject(
                _activationSignal,
                static (state, timedOut) =>
                {
                    if (!timedOut && state is SingleInstanceCoordinator coordinator)
                    {
                        coordinator.OnActivationRequested();
                    }
                },
                this,
                Timeout.Infinite,
                executeOnlyOnce: false);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal event EventHandler? ActivationRequested;

    internal bool IsPrimary { get; }

    internal static SingleInstanceCoordinator CreateForCurrentUser()
    {
        string userIdentity;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            userIdentity = identity.User?.Value ??
                $@"{Environment.UserDomainName}\{Environment.UserName}";
        }
        catch
        {
            // The app is Windows-only, but retaining a stable fallback keeps a
            // damaged identity lookup from allowing duplicate windows.
            userIdentity = $@"{Environment.UserDomainName}\{Environment.UserName}";
        }

        return new SingleInstanceCoordinator(
            $"CodingAgentAccountSwitcher:{userIdentity}");
    }

    internal bool NotifyPrimary()
    {
        if (IsPrimary || Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        try
        {
            return _activationSignal.Set();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _registeredWait?.Unregister(null);
        _releaseOwnership.Set();
        _ownershipThread.Join();
        _activationSignal.Dispose();
        _instanceMarker.Dispose();
        _releaseOwnership.Dispose();
    }

    private void HoldInstanceOwnership(TaskCompletionSource<bool> election)
    {
        var acquired = false;
        try
        {
            try
            {
                acquired = _instanceMarker.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // A crashed primary releases ownership without closing every
                // secondary handle. Windows transfers the abandoned lock here.
                acquired = true;
            }

            election.SetResult(acquired);
            if (acquired)
            {
                _releaseOwnership.Wait();
            }
        }
        catch (Exception exception)
        {
            election.TrySetException(exception);
        }
        finally
        {
            if (acquired)
            {
                _instanceMarker.ReleaseMutex();
            }
        }
    }

    private void OnActivationRequested()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            // Reset is redundant for the expected auto-reset event, but also
            // prevents a pre-existing manual-reset object with the same name
            // from causing a tight callback loop.
            _activationSignal.Reset();
            ActivationRequested?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // An activation callback must never terminate the process or stop
            // later launches from notifying the primary instance.
        }
    }
}
