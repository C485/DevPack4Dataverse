using C485.DataverseClientProxy.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace C485.DataverseClientProxy.Interfaces;

public interface IDataverseConnectionLayer
{
    Guid CreateRecord(Entity record, RequestSettings requestSettings = null);

    Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings = null);

    void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings = null);

    void DeleteRecord(EntityReference entityReference, RequestSettings requestSettings = null);

    Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings = null);

    Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings = null);

    T Execute<T>(OrganizationRequest request, RequestSettings requestSettings = null) where T : OrganizationResponse;

    ExecuteMultipleResponse Execute(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, RequestSettings requestSettings = null);

    Task<ExecuteMultipleResponse> ExecuteAsync(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, RequestSettings requestSettings = null);

    Task<T> ExecuteAsync<T>(OrganizationRequest request, RequestSettings requestSettings = null) where T : OrganizationResponse;

    Entity RefreshRecord(Entity record);

    Task<Entity> RefreshRecordAsync(Entity record);

    Entity Retrieve(string entityName, Guid id, ColumnSet columnSet);

    Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet);

    Entity[] RetrieveMultiple(QueryExpression queryExpression);

    Task<Entity[]> RetrieveMultipleAsync(QueryExpression queryExpression);

    Guid UpdateRecord(Entity record, RequestSettings requestSettings = null);

    Task<Guid> UpdateRecordAsync(Entity record, RequestSettings requestSettings = null);

    EntityReference UpsertRecord(Entity record, RequestSettings requestSettings = null);

    Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings = null);
}