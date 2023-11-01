namespace TemporalioSamples.Mutex.Impl;

internal record LockRequest(string InitiatorId, string AcquireLockSignalName, TimeSpan? Timeout = null);
