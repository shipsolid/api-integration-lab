namespace ApiIntegrationLab.UnitTests.TestDoubles;

internal sealed class ConcurrentStartProbe
{
    private readonly int _expectedStarts;
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _started;

    public ConcurrentStartProbe(int expectedStarts)
    {
        _expectedStarts = expectedStarts;
    }

    public bool AllStartedBeforeRelease { get; private set; }

    public async Task<T> StartAsync<T>(T result, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _started) == _expectedStarts)
        {
            AllStartedBeforeRelease = true;
            _release.TrySetResult();
        }

        await _release.Task.WaitAsync(cancellationToken);
        return result;
    }
}
