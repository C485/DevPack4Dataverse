using C485.DataverseClientProxy.Models;
using Microsoft.Xrm.Sdk;
using System;
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

        Guid UpdateRecord(Entity record, RequestSettings requestSettings);

        Task<Guid> UpdateRecordAsync(Entity record, RequestSettings requestSettings);

        EntityReference UpsertRecord(Entity record, RequestSettings requestSettings);

        Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings);
    }
}