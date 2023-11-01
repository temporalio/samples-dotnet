namespace TemporalioSamples.Mutex.Impl;

public interface ILockHandle : IAsyncDisposable
{
    public string LockInitiatorId { get; }

    public string ResourceId { get; }

    public string ReleaseSignalName { get; }
}
