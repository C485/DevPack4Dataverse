using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Models;

namespace DevPack4Dataverse.Tests.TestDoubles;

public sealed class FakeConnection : IConnection
{
    public int ReleaseCount { get; private set; }
    public int TryLockCount { get; private set; }
    public bool TryLockResult { get; set; } = true;
    public ulong Weight { get; set; }
    public RequestSettings LastRequestSettings { get; private set; }
    public Func<string, Guid, ColumnSet, Entity> RetrieveHandler { get; set; }
    public Func<OrganizationRequest, OrganizationResponse> ExecuteHandler { get; set; }
    public Func<Entity, Guid> CreateRecordHandler { get; set; }
    public Action<Entity> UpdateRecordHandler { get; set; }
    public Func<QueryExpression, Entity[]> RetrieveMultipleHandler { get; set; }

    public ServiceClient PureServiceClient => null;
    public IStatistics Statistics => null;

    public void ApplyConnectionOptimalization() { }
    public ulong GetConnectionWeight() => Weight;
    public void ReleaseLock() => ReleaseCount++;
    public bool Test() => true;
    public Task<bool> TestAsync() => Task.FromResult(true);

    public bool TryLock()
    {
        TryLockCount++;
        return TryLockResult;
    }

    public Task<bool> TryLockAsync()
    {
        TryLockCount++;
        return Task.FromResult(TryLockResult);
    }

    public Guid CreateRecord(Entity record, RequestSettings requestSettings = null)
    {
        LastRequestSettings = requestSettings;
        return CreateRecordHandler?.Invoke(record)
        ?? throw new NotImplementedException();
    }

    public Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings = null) =>
        Task.FromResult(CreateRecord(record, requestSettings));

    public void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public void DeleteRecord(EntityReference entityReference, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public T Execute<T>(OrganizationRequest request, RequestSettings requestSettings = null)
        where T : OrganizationResponse
    {
        LastRequestSettings = requestSettings;
        return (T)(ExecuteHandler?.Invoke(request) ?? throw new NotImplementedException());
    }

    public ExecuteMultipleResponse Execute(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        RequestSettings requestSettings = null) => throw new NotImplementedException();

    public Task<ExecuteMultipleResponse> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        RequestSettings requestSettings = null) => throw new NotImplementedException();

    public Task<T> ExecuteAsync<T>(OrganizationRequest request, RequestSettings requestSettings = null)
        where T : OrganizationResponse =>
        Task.FromResult(Execute<T>(request, requestSettings));

    public Entity RefreshRecord(Entity record, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public T RefreshRecord<T>(T record, RequestSettings requestSettings = null) where T : Entity =>
        throw new NotImplementedException();

    public Task<Entity> RefreshRecordAsync(Entity record, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public Task<T> RefreshRecordAsync<T>(T record, RequestSettings requestSettings = null) where T : Entity =>
        throw new NotImplementedException();

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet, RequestSettings requestSettings = null) =>
        RetrieveHandler?.Invoke(entityName, id, columnSet) ?? throw new NotImplementedException();

    public T Retrieve<T>(string entityName, Guid id, ColumnSet columnSet, RequestSettings requestSettings = null)
        where T : Entity => throw new NotImplementedException();

    public Task<Entity> RetrieveAsync(
        string entityName,
        Guid id,
        ColumnSet columnSet,
        RequestSettings requestSettings = null)
    {
        LastRequestSettings = requestSettings;
        return Task.FromResult(Retrieve(entityName, id, columnSet, requestSettings));
    }

    public Task<T> RetrieveAsync<T>(
        string entityName,
        Guid id,
        ColumnSet columnSet,
        RequestSettings requestSettings = null) where T : Entity => throw new NotImplementedException();

    public Entity[] RetrieveMultiple(QueryExpression queryExpression, RequestSettings requestSettings = null)
    {
        LastRequestSettings = requestSettings;
        return RetrieveMultipleHandler?.Invoke(queryExpression)
        ?? throw new NotImplementedException();
    }

    public T[] RetrieveMultiple<T>(QueryExpression queryExpression, RequestSettings requestSettings = null)
        where T : Entity => RetrieveMultiple(queryExpression, requestSettings).Cast<T>().ToArray();

    public Task<Entity[]> RetrieveMultipleAsync(
        QueryExpression queryExpression,
        RequestSettings requestSettings = null) =>
        Task.FromResult(RetrieveMultiple(queryExpression, requestSettings));

    public Task<T[]> RetrieveMultipleAsync<T>(
        QueryExpression queryExpression,
        RequestSettings requestSettings = null) where T : Entity =>
        Task.FromResult(RetrieveMultiple<T>(queryExpression, requestSettings));

    public void UpdateRecord(Entity record, RequestSettings requestSettings = null)
    {
        LastRequestSettings = requestSettings;
        if (UpdateRecordHandler == null)
        {
            throw new NotImplementedException();
        }

        UpdateRecordHandler(record);
    }

    public Task UpdateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        UpdateRecord(record, requestSettings);
        return Task.CompletedTask;
    }

    public EntityReference UpsertRecord(Entity record, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();
}
