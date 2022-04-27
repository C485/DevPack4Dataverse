using Ardalis.GuardClauses;
using C485.DataverseClientProxy.Interfaces;
using System;

namespace C485.DataverseClientProxy
{
    public class ConnectionLease : IDisposable
    {
        private bool disposedValue;

        public ConnectionLease(IConnection connection)
        {
            Connection = connection;
            Guard
                .Against
                .NullOrInvalidInput(connection, nameof(connection), p => p.IsLockedByThisThread());
        }

        ~ConnectionLease()
        {
            Dispose(disposing: false);
        }

        public IConnection Connection { get; }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposedValue)
            {
                return;
            }
            Connection
                .ReleaseLock();
            disposedValue = true;
        }
    }
}