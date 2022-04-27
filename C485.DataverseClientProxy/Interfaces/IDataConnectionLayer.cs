using C485.DataverseClientProxy.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace C485.DataverseClientProxy.Interfaces
{
    public interface IDataConnectionLayer
    {
        Guid CreateRecord(Entity record, RequestSettings requestSettings);

        Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings);

        void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings);

        void DeleteRecord(EntityReference entityReference, RequestSettings requestSettings);

        Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings);

        Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings);

        OrganizationResponse Execute(OrganizationRequest request, RequestSettings requestSettings);

        OrganizationResponse Execute(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder);

        Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request, RequestSettings requestSettings);

        Task<OrganizationResponse> ExecuteAsync(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder);

        Entity RefreshRecord(Entity record);

        Task<Entity> RefreshRecordAsync(Entity record);

        Entity Retrive(string entityName, Guid id, ColumnSet columnSet);

        Task<Entity> RetriveAsync(string entityName, Guid id, ColumnSet columnSet);

        IEnumerable<Entity> RetriveMultiple(QueryExpression queryExpression);

        Task<Entity[]> RetriveMultipleAsync(QueryExpression queryExpression);

        Guid UpdateRecord(Entity record, RequestSettings requestSettings);

        Task<Guid> UpdateRecordAsync(Entity record, RequestSettings requestSettings);

        EntityReference UpsertRecord(Entity record, RequestSettings requestSettings);

        Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings);
    }
}