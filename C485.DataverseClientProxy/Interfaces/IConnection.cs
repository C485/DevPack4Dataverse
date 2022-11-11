namespace C485.DataverseClientProxy.Interfaces;

public interface IConnection : IDataverseConnectionLayer
{
    IConnection DisableLockingCheck();

    bool IsLockedByThisThread();

    void ReleaseLock();

    bool Test();

    Task<bool> TestAsync();

    bool TryLock();
}