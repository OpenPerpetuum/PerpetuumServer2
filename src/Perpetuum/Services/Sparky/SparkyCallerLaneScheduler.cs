namespace Perpetuum.Services.Sparky;

/// <summary>
/// Runs accepted work in order for each source/caller pair while allowing unrelated callers to proceed concurrently.
/// </summary>
internal sealed class SparkyCallerLaneScheduler
{
    private readonly object _lock = new();
    private readonly Dictionary<SparkyRequestIdentity, Task> _tails = new();

    public Task Schedule(SparkyRequestIdentity identity, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (_lock)
        {
            _tails.TryGetValue(identity, out var previous);
            var scheduled = RunAfterAsync(previous, action);
            _tails[identity] = scheduled;
            _ = scheduled.ContinueWith(
                completed =>
                {
                    _ = completed.Exception;
                    RemoveCompleted(identity, completed);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return scheduled;
        }
    }

    public async Task WaitForIdleAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task[] pending;
            lock (_lock)
            {
                pending = _tails.Values.ToArray();
            }

            if (pending.Length == 0)
            {
                return;
            }

            try
            {
                await Task.WhenAll(pending).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // A failed lane is still drained; its successor has already been scheduled to continue afterward.
            }
        }
    }

    private static async Task RunAfterAsync(Task? previous, Func<Task> action)
    {
        if (previous is not null)
        {
            try
            {
                await previous.ConfigureAwait(false);
            }
            catch
            {
                // A failed request must not prevent later requests in the same caller lane.
            }
        }

        await Task.Yield();
        await action().ConfigureAwait(false);
    }

    private void RemoveCompleted(SparkyRequestIdentity identity, Task completed)
    {
        lock (_lock)
        {
            if (_tails.TryGetValue(identity, out var current) && ReferenceEquals(current, completed))
            {
                _tails.Remove(identity);
            }
        }
    }
}
