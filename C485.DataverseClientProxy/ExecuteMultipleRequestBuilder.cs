using System;
using Ardalis.GuardClauses;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace C485.DataverseClientProxy;

public class ExecuteMultipleRequestBuilder
{
	public ExecuteMultipleRequestBuilder(
		bool continueOnError = true,
		bool skipPluginExecution = false,
		Guid? impersonateAs = null)
	{
		SkipPluginExecution = skipPluginExecution;
		ImpersonateAsUserById = impersonateAs;
		RequestWithResults = new ExecuteMultipleRequest
		{
			Settings = new ExecuteMultipleSettings
			{
				ContinueOnError = continueOnError,
				ReturnResponses = true
			},
			Requests = new OrganizationRequestCollection()
		};
	}

	public int Count => RequestWithResults.Requests.Count;

	public Guid? ImpersonateAsUserById { get; }

	public ExecuteMultipleRequest RequestWithResults { get; }

	public bool SkipPluginExecution { get; }

	public void AddCreate(Entity record)
	{
		Guard
		   .Against
		   .NullOrInvalidInput(record, nameof(record), p => p.Id == Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

		CreateRequest request = new()
		{
			Target = record
		};

		RequestWithResults
		   .Requests
		   .Add(request);
	}

	public void AddDelete(EntityReference entityReference)
	{
		Guard
		   .Against
		   .NullOrInvalidInput(entityReference,
				nameof(entityReference),
				p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

		DeleteRequest request = new()
		{
			Target = entityReference
		};

		RequestWithResults
		   .Requests
		   .Add(request);
	}

	public void AddDelete(string logicalName, Guid id)
	{
		Guard
		   .Against
		   .NullOrEmpty(logicalName, nameof(logicalName));

		Guard
		   .Against
		   .Default(id, nameof(id));

		AddDelete(new EntityReference(logicalName, id));
	}

	public void AddRequest(OrganizationRequest request)
	{
		Guard
		   .Against
		   .Null(request, nameof(request));

		RequestWithResults
		   .Requests
		   .Add(request);
	}

	public void AddUpdate(Entity record)
	{
		Guard
		   .Against
		   .NullOrInvalidInput(record, nameof(record), p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

		UpdateRequest request = new()
		{
			Target = record
		};

		RequestWithResults
		   .Requests
		   .Add(request);
	}

	public void AddUpsert(Entity record)
	{
		Guard
		   .Against
		   .NullOrInvalidInput(record, nameof(record), p => !string.IsNullOrEmpty(p.LogicalName));

		UpsertRequest request = new()
		{
			Target = record
		};

		RequestWithResults
		   .Requests
		   .Add(request);
	}
}