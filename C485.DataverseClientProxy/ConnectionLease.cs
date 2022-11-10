using Ardalis.GuardClauses;
using C485.DataverseClientProxy.Interfaces;

namespace C485.DataverseClientProxy;

public sealed class ConnectionLease : IDisposable
{
    private bool _disposedValue;

    public ConnectionLease(IConnection connection)
    {
        Connection = connection;
        Guard
           .Against
           .NullOrInvalidInput(connection, nameof(connection), p => p.IsLockedByThisThread());
    }

    ~ConnectionLease()
    {
        Dispose(false);
    }

    public IConnection Connection { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposedValue)
        {
            return;
        }

        Connection
           .ReleaseLock();

        _disposedValue = true;
    }
}