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
    public Func<string, Guid, ColumnSet, Entity> RetrieveHandler { get; set; }
    public Func<OrganizationRequest, OrganizationResponse> ExecuteHandler { get; set; }

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

    public Guid CreateRecord(Entity record, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public void DeleteRecord(EntityReference entityReference, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public T Execute<T>(OrganizationRequest request, RequestSettings requestSettings = null)
        where T : OrganizationResponse =>
        (T)(ExecuteHandler?.Invoke(request) ?? throw new NotImplementedException());

    public ExecuteMultipleResponse Execute(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        RequestSettings requestSettings = null) => throw new NotImplementedException();

    public Task<ExecuteMultipleResponse> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        RequestSettings requestSettings = null) => throw new NotImplementedException();

    public Task<T> ExecuteAsync<T>(OrganizationRequest request, RequestSettings requestSettings = null)
        where T : OrganizationResponse =>
        Task.FromResult((T)(ExecuteHandler?.Invoke(request) ?? throw new NotImplementedException()));

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
        RequestSettings requestSettings = null) =>
        Task.FromResult(Retrieve(entityName, id, columnSet));

    public Task<T> RetrieveAsync<T>(
        string entityName,
        Guid id,
        ColumnSet columnSet,
        RequestSettings requestSettings = null) where T : Entity => throw new NotImplementedException();

    public Entity[] RetrieveMultiple(QueryExpression queryExpression, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public T[] RetrieveMultiple<T>(QueryExpression queryExpression, RequestSettings requestSettings = null)
        where T : Entity => throw new NotImplementedException();

    public Task<Entity[]> RetrieveMultipleAsync(
        QueryExpression queryExpression,
        RequestSettings requestSettings = null) => throw new NotImplementedException();

    public Task<T[]> RetrieveMultipleAsync<T>(
        QueryExpression queryExpression,
        RequestSettings requestSettings = null) where T : Entity => throw new NotImplementedException();

    public void UpdateRecord(Entity record, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public Task UpdateRecordAsync(Entity record, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public EntityReference UpsertRecord(Entity record, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();

    public Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings = null) =>
        throw new NotImplementedException();
}
