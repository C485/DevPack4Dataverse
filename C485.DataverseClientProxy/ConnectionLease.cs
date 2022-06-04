using System;
using Ardalis.GuardClauses;
using C485.DataverseClientProxy.Interfaces;

namespace C485.DataverseClientProxy;

public class ConnectionLease : IDisposable
{
	private bool _disposedValue;

	public ConnectionLease(IConnection connection)
	{
		Connection = connection;
		Guard
		   .Against
		   .NullOrInvalidInput(connection, nameof(connection), p => p.IsLockedByThisThread());
	}

	public IConnection Connection { get; }

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	~ConnectionLease()
	{
		Dispose(false);
	}

	protected virtual void Dispose(bool disposing)
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