using C485.DataverseClientProxy.Interfaces;
using C485.DataverseClientProxy.Logic;
using C485.DataverseClientProxy.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace C485.DataverseClientProxy;

public sealed class DataverseClientProxy : IDataverseClientProxy
{
    private readonly ConnectionManager _connectionManager;
    public readonly ExecuteMultipleLogic ExecuteMultipleLogic;

    public DataverseClientProxy()
    {
        _connectionManager = new ConnectionManager();
        ExecuteMultipleLogic = new ExecuteMultipleLogic(_connectionManager);
    }

    public Guid CreateRecord(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = _connectionManager.GetConnection();

        return connectionLease
           .Connection
           .CreateRecord(record, requestSettings);
    }

    public async Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await _connectionManager.GetConnectionAsync();

        return await connectionLease
           .Connection
           .CreateRecordAsync(record, requestSettings);
    }

    public void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = _connectionManager.GetConnection();

        connectionLease
           .Connection
           .DeleteRecord(logicalName, id, requestSettings);
    }

    public void DeleteRecord(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = _connectionManager.GetConnection();

        connectionLease
           .Connection
           .DeleteRecord(entityReference, requestSettings);
    }

    public async Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await _connectionManager.GetConnectionAsync();

        await connectionLease
           .Connection
           .DeleteRecordAsync(logicalName, id, requestSettings);
    }

    public async Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await _connectionManager.GetConnectionAsync();
        await connectionLease
           .Connection
           .DeleteRecordAsync(entityReference, requestSettings);
    }

    public T Execute<T>(OrganizationRequest request, RequestSettings requestSettings = null) where T : OrganizationResponse
    {
        using ConnectionLease connectionLease = _connectionManager.GetConnection();

        return connectionLease
           .Connection
           .Execute<T>(request, requestSettings);
    }

    public ExecuteMultipleResponse Execute(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = _connectionManager.GetConnection();

        return connectionLease
           .Connection
           .Execute(executeMultipleRequestBuilder, requestSettings);
    }

    public async Task<ExecuteMultipleResponse> ExecuteAsync(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await _connectionManager.GetConnectionAsync();

        return await connectionLease
           .Connection
           .ExecuteAsync(executeMultipleRequestBuilder, requestSettings);
    }

    public async Task<T> ExecuteAsync<T>(OrganizationRequest request, RequestSettings requestSettings = null) where T : OrganizationResponse
    {
        using ConnectionLease connectionLease = await _connectionManager.GetConnectionAsync();

        return await connectionLease
           .Connection
           .ExecuteAsync<T>(request, requestSettings);
    }

    public Entity RefreshRecord(Entity record)
    {
        using ConnectionLease connectionLease = _connectionManager.GetConnection();

        return connectionLease
           .Connection
           .RefreshRecord(record);
    }

    public async Task<Entity> RefreshRecordAsync(Entity record)
    {
        using ConnectionLease connectionLease = await _connectionManager.GetConnectionAsync();

        return await connectionLease
           .Connection
           .RefreshRecordAsync(record);
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
    {
        using ConnectionLease connectionLease = _connectionManager.GetConnection();

        return connectionLease
           .Connection
           .Retrieve(entityName, id, columnSet);
    }

    public async Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet)
    {
        using ConnectionLease connectionLease = await _connectionManager.GetConnectionAsync();

        return await connectionLease
           .Connection
           .RetrieveAsync(entityName, id, columnSet);
    }

    public Entity[] RetrieveMultiple(QueryExpression queryExpression)
    {
        using ConnectionLease connectionLease = _connectionManager.GetConnection();

        return connectionLease
           .Connection
           .RetrieveMultiple(queryExpression);
    }

    public async Task<Entity[]> RetrieveMultipleAsync(QueryExpression queryExpression)
    {
        using ConnectionLease connectionLease = await _connectionManager.GetConnectionAsync();

        return await connectionLease
           .Connection
           .RetrieveMultipleAsync(queryExpression);
    }

    public Guid UpdateRecord(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = _connectionManager.GetConnection();

        return connectionLease
           .Connection
           .UpdateRecord(record, requestSettings);
    }

    public async Task<Guid> UpdateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await _connectionManager.GetConnectionAsync();

        return await connectionLease
           .Connection
           .UpdateRecordAsync(record, requestSettings);
    }

    public EntityReference UpsertRecord(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = _connectionManager.GetConnection();

        return connectionLease
           .Connection
           .UpsertRecord(record, requestSettings);
    }

    public async Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await _connectionManager.GetConnectionAsync();

        return await connectionLease
           .Connection
           .UpsertRecordAsync(record, requestSettings);
    }
}