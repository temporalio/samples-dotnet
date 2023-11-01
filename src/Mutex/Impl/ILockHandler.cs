namespace TemporalioSamples.Mutex.Impl;

internal interface ILockHandler
{
    public string? CurrentOwnerId { get; }

    public Task HandleAsync(LockRequest lockRequest);
}
