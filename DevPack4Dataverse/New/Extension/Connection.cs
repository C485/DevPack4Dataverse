using CommunityToolkit.Diagnostics;
using DevPack4Dataverse.Utils;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.New.Extension;

public class ServiceClientExtensionsInner
{
    private readonly ServiceClient _connection;

    internal ServiceClientExtensionsInner(ServiceClient connection)
    {
        Guard.IsNotNull(connection);
        _connection = connection;
    }

    public async Task<T> ExecuteAsync<T>(OrganizationRequest request)
        where T : OrganizationResponse
    {
        Guard.IsNotNull(request);

        return await _connection.ExecuteAsync(request) as T;
    }

    public async Task<ExecuteMultipleResponse> ExecuteAsync(ExecuteMultipleRequest executeMultipleRequest)
    {
        Guard.IsNotNull(executeMultipleRequest);
        Guard.IsGreaterThan(executeMultipleRequest.Requests.Count, 5000);

        return await ExecuteAsync<ExecuteMultipleResponse>(executeMultipleRequest);
    }

    public async Task<T> RefreshRecordAsync<T>(T record) where T : Entity
    {
        Guard.IsNotNull(record);
        Guard.IsNotNullOrEmpty(record.LogicalName);
        Guard.IsNotDefault(record.Id);

        ColumnSet columns = new(record.Attributes.Keys.ToArray());

        return (await _connection.RetrieveAsync(record.LogicalName, record.Id, columns)).ToEntity<T>();
    }

    public async Task<Entity> RefreshRecordAsync(Entity record)
    {
        Guard.IsNotNull(record);
        Guard.IsNotNullOrEmpty(record.LogicalName);
        Guard.IsNotDefault(record.Id);

        ColumnSet columns = new(record.Attributes.Keys.ToArray());

        return await _connection.RetrieveAsync(record.LogicalName, record.Id, columns);
    }

    public async Task<Entity[]> RetrieveMultipleAsync(QueryExpression queryExpression)
    {
        Guard.IsNotNull(queryExpression);

        return await InnerRetrieveMultiple().ToArrayAsync();

        async IAsyncEnumerable<Entity> InnerRetrieveMultiple()
        {
            queryExpression.PageInfo = new PagingInfo
            {
                Count = 5000,
                PageNumber = 1,
                PagingCookie = null
            };

            while (true)
            {
                RetrieveMultipleResponse retrieveMultipleResponse = await ExecuteAsync<RetrieveMultipleResponse>(
                    new RetrieveMultipleRequest
                    {
                        Query = queryExpression
                    });

                Guard.IsNotNull(retrieveMultipleResponse);
                EntityCollection retrieveMultipleResult = retrieveMultipleResponse.EntityCollection;

                foreach (Entity record in retrieveMultipleResult.Entities)
                {
                    yield return record;
                }

                if (!retrieveMultipleResult.MoreRecords)
                {
                    break;
                }

                queryExpression.PageInfo.PageNumber++;
                queryExpression.PageInfo.PagingCookie = retrieveMultipleResult.PagingCookie;
            }
        }
    }

    public async Task<T[]> RetrieveMultipleAsync<T>(QueryExpression queryExpression) where T : Entity
    {
        Guard.IsNotNull(queryExpression);

        return await InnerRetrieveMultiple().ToArrayAsync();

        async IAsyncEnumerable<T> InnerRetrieveMultiple()
        {
            queryExpression.PageInfo = new PagingInfo
            {
                Count = 5000,
                PageNumber = 1,
                PagingCookie = null
            };

            while (true)
            {
                RetrieveMultipleResponse retrieveMultipleResponse = await ExecuteAsync<RetrieveMultipleResponse>(
                    new RetrieveMultipleRequest
                    {
                        Query = queryExpression
                    });

                Guard.IsNotNull(retrieveMultipleResponse);
                EntityCollection retrieveMultipleResult = retrieveMultipleResponse.EntityCollection;

                foreach (T record in retrieveMultipleResult.Entities.Select(p => p.ToEntity<T>()))
                {
                    yield return record;
                }

                if (!retrieveMultipleResult.MoreRecords)
                {
                    break;
                }

                queryExpression.PageInfo.PageNumber++;
                queryExpression.PageInfo.PagingCookie = retrieveMultipleResult.PagingCookie;
            }
        }
    }

    public T Execute<T>(OrganizationRequest request)
        where T : OrganizationResponse
    {
        Guard.IsNotNull(request);

        return _connection.Execute(request) as T;
    }

    public ExecuteMultipleResponse Execute(ExecuteMultipleRequest executeMultipleRequest)
    {
        Guard.IsNotNull(executeMultipleRequest);
        Guard.IsGreaterThan(executeMultipleRequest.Requests.Count, 5000);

        return Execute<ExecuteMultipleResponse>(executeMultipleRequest);
    }

    public Entity RefreshRecord(Entity record)
    {
        Guard.IsNotNull(record);
        Guard.IsNotNullOrEmpty(record.LogicalName);
        Guard.IsNotDefault(record.Id);

        ColumnSet columns = new(record.Attributes.Keys.ToArray());

        return Retrieve(record.LogicalName, record.Id, columns);
    }

    public T RefreshRecord<T>(T record) where T : Entity
    {
        Guard.IsNotNull(record);
        Guard.IsNotNullOrEmpty(record.LogicalName);
        Guard.IsNotDefault(record.Id);

        ColumnSet columns = new(record.Attributes.Keys.ToArray());

        return Retrieve<T>(record.LogicalName, record.Id, columns);
    }

    public Entity Retrieve(
        string logicalName,
        Guid id,
        ColumnSet columnSet)
    {
        Guard.IsNotNullOrEmpty(logicalName);
        Guard.IsNotDefault(id);
        Guard.IsNotNull(columnSet);

        RetrieveResponse retrieveResponse = Execute<RetrieveResponse>(new RetrieveRequest
        {
            ColumnSet = columnSet,
            Target = EntityReferenceUtils.CreateEntityReference(id, logicalName)
        });

        Guard.IsNotNull(retrieveResponse);

        return retrieveResponse.Entity;
    }

    public T Retrieve<T>(
        string logicalName,
        Guid id,
        ColumnSet columnSet)
        where T : Entity
    {
        return Retrieve(logicalName, id, columnSet)?.ToEntity<T>();
    }

    public Entity[] RetrieveMultiple(QueryExpression queryExpression)
    {
        Guard.IsNotNull(queryExpression);

        return InnerRetrieveMultiple().ToArray();

        IEnumerable<Entity> InnerRetrieveMultiple()
        {
            queryExpression.PageInfo = new PagingInfo
            {
                Count = 5000,
                PageNumber = 1,
                PagingCookie = null
            };

            while (true)
            {
                RetrieveMultipleResponse retrieveMultipleResponse = Execute<RetrieveMultipleResponse>(
                    new RetrieveMultipleRequest
                    {
                        Query = queryExpression
                    });

                Guard.IsNotNull(retrieveMultipleResponse);
                EntityCollection retrieveMultipleResult = retrieveMultipleResponse.EntityCollection;

                foreach (Entity record in retrieveMultipleResult.Entities)
                {
                    yield return record;
                }

                if (!retrieveMultipleResult.MoreRecords)
                {
                    break;
                }

                queryExpression.PageInfo.PageNumber++;
                queryExpression.PageInfo.PagingCookie = retrieveMultipleResult.PagingCookie;
            }
        }
    }

    public T[] RetrieveMultiple<T>(QueryExpression queryExpression)
        where T : Entity
    {
        Guard.IsNotNull(queryExpression);

        return InnerRetrieveMultiple().ToArray();

        IEnumerable<T> InnerRetrieveMultiple()
        {
            queryExpression.PageInfo = new PagingInfo
            {
                Count = 5000,
                PageNumber = 1,
                PagingCookie = null
            };

            while (true)
            {
                RetrieveMultipleResponse retrieveMultipleResponse = Execute<RetrieveMultipleResponse>(
                    new RetrieveMultipleRequest
                    {
                        Query = queryExpression
                    });

                Guard.IsNotNull(retrieveMultipleResponse);
                EntityCollection retrieveMultipleResult = retrieveMultipleResponse.EntityCollection;

                foreach (T record in retrieveMultipleResult.Entities.Select(p => p.ToEntity<T>()))
                {
                    yield return record;
                }

                if (!retrieveMultipleResult.MoreRecords)
                {
                    break;
                }

                queryExpression.PageInfo.PageNumber++;
                queryExpression.PageInfo.PagingCookie = retrieveMultipleResult.PagingCookie;
            }
        }
    }
}
