/*
Copyright 2022 Kamil Skoracki / C485@GitHub

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using DevPack4Dataverse.Models;
using DevPack4Dataverse.New.ExecuteMultiple;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.Interfaces;
//
//public interface IDataverseConnectionLayer
//{
//    Guid CreateRecord(Entity record, RequestSettings requestSettings = null);
//
//    Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings = null);
//
//    void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings = null);
//
//    void DeleteRecord(EntityReference entityReference, RequestSettings requestSettings = null);
//
//    Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings = null);
//
//    Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings = null);
//
//    T Execute<T>(OrganizationRequest request, RequestSettings requestSettings = null) where T : OrganizationResponse;
//
//    ExecuteMultipleResponse Execute(
//        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
//        RequestSettings requestSettings = null
//    );
//
//    Task<ExecuteMultipleResponse> ExecuteAsync(
//        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
//        RequestSettings requestSettings = null
//    );
//
//    Task<T> ExecuteAsync<T>(OrganizationRequest request, RequestSettings requestSettings = null)
//        where T : OrganizationResponse;
//
//    Entity RefreshRecord(Entity record, RequestSettings requestSettings = null);
//
//    T RefreshRecord<T>(T record, RequestSettings requestSettings = null) where T : Entity;
//
//    Task<Entity> RefreshRecordAsync(Entity record, RequestSettings requestSettings = null);
//
//    Task<T> RefreshRecordAsync<T>(T record, RequestSettings requestSettings = null) where T : Entity;
//
//    Entity Retrieve(string entityName, Guid id, ColumnSet columnSet, RequestSettings requestSettings = null);
//
//    T Retrieve<T>(string entityName, Guid id, ColumnSet columnSet, RequestSettings requestSettings = null)
//        where T : Entity;
//
//    Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet, RequestSettings requestSettings = null);
//
//    Task<T> RetrieveAsync<T>(string entityName, Guid id, ColumnSet columnSet, RequestSettings requestSettings = null)
//        where T : Entity;
//
//    Entity[] RetrieveMultiple(QueryExpression queryExpression, RequestSettings requestSettings = null);
//
//    T[] RetrieveMultiple<T>(QueryExpression queryExpression, RequestSettings requestSettings = null) where T : Entity;
//
//    Task<Entity[]> RetrieveMultipleAsync(QueryExpression queryExpression, RequestSettings requestSettings = null);
//
//    Task<T[]> RetrieveMultipleAsync<T>(QueryExpression queryExpression, RequestSettings requestSettings = null)
//        where T : Entity;
//
//    void UpdateRecord(Entity record, RequestSettings requestSettings = null);

    Task UpdateRecordAsync(Entity record, RequestSettings requestSettings = null);

    EntityReference UpsertRecord(Entity record, RequestSettings requestSettings = null);

    Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings = null);
}
