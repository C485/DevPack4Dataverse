using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using C485.DataverseClientProxy.Interfaces;
using C485.DataverseClientProxy.Models;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;

namespace C485.DataverseClientProxy;

public class Connection : IConnection
{
	private readonly CrmServiceClient _connection;
	private readonly object _lockObj;
	private readonly OrganizationServiceContext _xrmServiceContext;
	private bool _disableLockingCheck;

	public Connection(CrmServiceClient connection)
	{
		_lockObj = new object();
		_connection = Guard
		   .Against
		   .Null(connection, nameof(connection));

		_xrmServiceContext = new OrganizationServiceContext(connection);
		_connection
		   .DisableCrossThreadSafeties = true;

		_connection
		   .MaxRetryCount = 10;

		_connection
		   .RetryPauseTime = TimeSpan.FromSeconds(5);
	}

	public IQueryable<Entity> CreateQuery_Unsafe_Unprotected(string entityLogicalName)
	{
		Guard
		   .Against
		   .NullOrEmpty(entityLogicalName, nameof(entityLogicalName));

		return _xrmServiceContext
		   .CreateQuery(entityLogicalName);
	}

	public Guid CreateRecord(Entity record, RequestSettings requestSettings)
	{
		Guard
		   .Against
		   .NullOrInvalidInput(record, nameof(record), p => p.Id == Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

		Guard
		   .Against
		   .Null(requestSettings, nameof(requestSettings));

		if (!_disableLockingCheck && !IsLockedByThisThread())
		{
			throw new ArgumentException("Lock not set for used connection.");
		}

		_connection
		   .CallerId = requestSettings.ImpersonateAsUserByDataverseId ?? Guid.Empty;

		_connection
		   .BypassPluginExecution = requestSettings.SkipPluginExecution;

		return Guard
		   .Against
		   .Null(_connection, nameof(_connection))
		   .Create(record);
	}

	public async Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings)
	{
		return await Task
		   .Run(() => CreateRecord(record, requestSettings));
	}

	public void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings)
	{
		Guard
		   .Against
		   .NullOrEmpty(logicalName, nameof(logicalName));

		Guard
		   .Against
		   .Default(id, nameof(id));

		Guard
		   .Against
		   .Null(requestSettings, nameof(requestSettings));

		if (!_disableLockingCheck && !IsLockedByThisThread())
		{
			throw new ArgumentException("Lock not set for used connection.");
		}

		_connection
		   .CallerId = requestSettings.ImpersonateAsUserByDataverseId ?? Guid.Empty;

		_connection
		   .BypassPluginExecution = requestSettings.SkipPluginExecution;

		Guard
		   .Against
		   .Null(_connection, nameof(_connection))
		   .Delete(logicalName, id);
	}

	public void DeleteRecord(EntityReference entityReference, RequestSettings requestSettings)
	{
		Guard
		   .Against
		   .Null(entityReference, nameof(entityReference));

		DeleteRecord(entityReference.LogicalName, entityReference.Id, requestSettings);
	}

	public async Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings)
	{
		await Task
		   .Run(() => DeleteRecord(logicalName, id, requestSettings));
	}

	public async Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings)
	{
		await Task
		   .Run(() => DeleteRecord(entityReference, requestSettings));
	}

	public void DisableLockingCheck()
	{
		_disableLockingCheck = true;
	}

	public OrganizationResponse Execute(OrganizationRequest request, RequestSettings requestSettings)
	{
		Guard
		   .Against
		   .Null(request, nameof(request));

		Guard
		   .Against
		   .Null(requestSettings, nameof(requestSettings));

		if (!_disableLockingCheck && !IsLockedByThisThread())
		{
			throw new ArgumentException("Lock not set for this connection.");
		}

		_connection
		   .CallerId = requestSettings.ImpersonateAsUserByDataverseId ?? Guid.Empty;

		_connection
		   .BypassPluginExecution = requestSettings.SkipPluginExecution;

		return Guard
		   .Against
		   .Null(_connection, nameof(_connection))
		   .Execute(request);
	}

	public OrganizationResponse Execute(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder)
	{
		Guard
		   .Against
		   .Null(executeMultipleRequestBuilder, nameof(executeMultipleRequestBuilder));

		return Execute(executeMultipleRequestBuilder.RequestWithResults,
			new RequestSettings
			{
				ImpersonateAsUserByDataverseId = executeMultipleRequestBuilder.ImpersonateAsUserById,
				SkipPluginExecution = executeMultipleRequestBuilder.SkipPluginExecution
			});
	}

	public async Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request, RequestSettings requestSettings)
	{
		return await Task
		   .Run(() => Execute(request, requestSettings));
	}

	public async Task<OrganizationResponse> ExecuteAsync(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder)
	{
		return await Task
		   .Run(() => Execute(executeMultipleRequestBuilder));
	}

	public bool IsLockedByThisThread()
	{
		return Monitor
		   .IsEntered(_lockObj);
	}

	public Entity[] QueryMultiple(string entityLogicalName, Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder)
	{
		Guard
		   .Against
		   .NullOrEmpty(entityLogicalName, nameof(entityLogicalName));

		Guard
		   .Against
		   .Null(queryBuilder, nameof(queryBuilder));

		IQueryable<Entity> query = _xrmServiceContext
		   .CreateQuery(entityLogicalName);

		return queryBuilder(query)
		   .ToArray();
	}

	public async Task<Entity[]> QueryMultipleAsync(
		string entityLogicalName,
		Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder)
	{
		return await Task
		   .Run(() => QueryMultiple(entityLogicalName, queryBuilder));
	}

	public Entity QuerySingle(string entityLogicalName, Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder)
	{
		Guard
		   .Against
		   .NullOrEmpty(entityLogicalName, nameof(entityLogicalName));

		Guard
		   .Against
		   .Null(queryBuilder, nameof(queryBuilder));

		IQueryable<Entity> query = _xrmServiceContext
		   .CreateQuery(entityLogicalName);

		return queryBuilder(query)
		   .Single();
	}

	public async Task<Entity> QuerySingleAsync(
		string entityLogicalName,
		Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder)
	{
		return await Task
		   .Run(() => QuerySingle(entityLogicalName, queryBuilder));
	}

	public Entity QuerySingleOrDefault(
		string entityLogicalName,
		Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder)
	{
		Guard
		   .Against
		   .NullOrEmpty(entityLogicalName, nameof(entityLogicalName));

		Guard
		   .Against
		   .Null(queryBuilder, nameof(queryBuilder));

		IQueryable<Entity> query = _xrmServiceContext
		   .CreateQuery(entityLogicalName);

		return queryBuilder(query)
		   .SingleOrDefault();
	}

	public async Task<Entity> QuerySingleOrDefaultAsync(
		string entityLogicalName,
		Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder)
	{
		return await Task
		   .Run(() => QuerySingleOrDefault(entityLogicalName, queryBuilder));
	}

	public Entity RefreshRecord(Entity record)
	{
		Guard
		   .Against
		   .NullOrInvalidInput(record, nameof(record), p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

		Guard
		   .Against
		   .Null(_connection, nameof(_connection));

		if (!_disableLockingCheck && !IsLockedByThisThread())
		{
			throw new ArgumentException("Lock not set for this connection.");
		}

		ColumnSet columns = new(false);

		foreach (string fieldName in record.Attributes.Keys)
		{
			columns.AddColumn(fieldName);
		}

		return _connection
		   .Retrieve(record.LogicalName, record.Id, columns);
	}

	public async Task<Entity> RefreshRecordAsync(Entity record)
	{
		return await Task
		   .Run(() => RefreshRecord(record));
	}

	public void ReleaseLock()
	{
		Monitor
		   .Exit(_lockObj);
	}

	public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
	{
		if (!_disableLockingCheck && !IsLockedByThisThread())
		{
			throw new ArgumentException("Lock not set for this connection.");
		}

		Guard
		   .Against
		   .Null(_connection, nameof(_connection));

		Guard
		   .Against
		   .NullOrEmpty(entityName, nameof(entityName));

		Guard
		   .Against
		   .Default(id, nameof(id));

		Guard
		   .Against
		   .Null(columnSet, nameof(columnSet));

		return _connection
		   .Retrieve(entityName, id, columnSet);
	}

	public async Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet)
	{
		return await Task
		   .Run(() => RetrieveAsync(entityName, id, columnSet));
	}

	public Entity[] RetrieveMultiple(QueryExpression queryExpression)
	{
		Guard
		   .Against
		   .Null(queryExpression, nameof(queryExpression));

		if (!_disableLockingCheck && !IsLockedByThisThread())
		{
			throw new ArgumentException("Lock not set for this connection.");
		}

		return InnerRetrieveMultiple()
		   .ToArray();

		IEnumerable<Entity> InnerRetrieveMultiple()
		{
			Guard
			   .Against
			   .Null(_connection, nameof(_connection));

			queryExpression.PageInfo = new PagingInfo
			{
				Count = 5000,
				PageNumber = 1,
				PagingCookie = null
			};

			while (true)
			{
				EntityCollection ret = _connection
				   .RetrieveMultiple(queryExpression);

				foreach (Entity record in ret.Entities)
				{
					yield return record;
				}

				if (!ret.MoreRecords)
				{
					break;
				}

				queryExpression.PageInfo.PageNumber++;
				queryExpression.PageInfo.PagingCookie = ret.PagingCookie;
			}
		}
	}

	public async Task<Entity[]> RetrieveMultipleAsync(QueryExpression queryExpression)
	{
		return await Task
		   .Run(() => RetrieveMultiple(queryExpression));
	}

	public bool Test()
	{
		Guard
		   .Against
		   .Null(_connection, nameof(_connection));

		WhoAmIResponse response = (WhoAmIResponse)_connection
		   .Execute(new WhoAmIRequest());

		return response != null
			&& response.UserId != Guid.Empty;
	}

	public async Task<bool> TestAsync()
	{
		return await Task
		   .Run(Test);
	}

	public bool TryLock()
	{
		return Monitor
		   .TryEnter(_lockObj);
	}

	public Guid UpdateRecord(Entity record, RequestSettings requestSettings)
	{
		Guard
		   .Against
		   .NullOrInvalidInput(record, nameof(record), p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

		Guard
		   .Against
		   .Null(requestSettings, nameof(requestSettings));

		if (!_disableLockingCheck && !IsLockedByThisThread())
		{
			throw new ArgumentException("Lock not set for used connection.");
		}

		_connection
		   .CallerId = requestSettings.ImpersonateAsUserByDataverseId ?? Guid.Empty;

		_connection
		   .BypassPluginExecution = requestSettings.SkipPluginExecution;

		Guard
		   .Against
		   .Null(_connection, nameof(_connection))
		   .Update(record);

		return record
		   .Id;
	}

	public async Task<Guid> UpdateRecordAsync(Entity record, RequestSettings requestSettings)
	{
		return await Task
		   .Run(() => UpdateRecord(record, requestSettings));
	}

	public EntityReference UpsertRecord(Entity record, RequestSettings requestSettings)
	{
		Guard
		   .Against
		   .NullOrInvalidInput(record, nameof(record), p => string.IsNullOrEmpty(p.LogicalName));

		Guard
		   .Against
		   .Null(requestSettings, nameof(requestSettings));

		if (!_disableLockingCheck && !IsLockedByThisThread())
		{
			throw new ArgumentException("Lock not set for used connection.");
		}

		_connection
		   .CallerId = requestSettings.ImpersonateAsUserByDataverseId ?? Guid.Empty;

		_connection
		   .BypassPluginExecution = requestSettings.SkipPluginExecution;

		UpsertRequest request = new()
		{
			Target = record
		};

		UpsertResponse executeResponse = (UpsertResponse)Execute(request, requestSettings);

		return Guard
		   .Against
		   .Null(executeResponse, nameof(executeResponse))
		   .Target;
	}

	public async Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings)
	{
		return await Task
		   .Run(() => UpsertRecord(record, requestSettings));
	}
}