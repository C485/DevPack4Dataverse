using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.New.Base;

public interface IAsyncConnection
{
    Task<bool> TestAsync();

    Task<ExecuteMultipleResponse> ExecuteAsync(ExecuteMultipleRequest executeMultipleRequest);

    Task<T> ExecuteAsync<T>(OrganizationRequest request)
        where T : OrganizationResponse;

    Task<Entity> RefreshRecordAsync(Entity record);

    Task<T> RefreshRecordAsync<T>(T record) where T : Entity;

    Task<Entity> RetrieveAsync(
        string logicalName,
        Guid id,
        ColumnSet columnSet);

    Task<T> RetrieveAsync<T>(
        string logicalName,
        Guid id,
        ColumnSet columnSet)
        where T : Entity;

    Task<Entity[]> RetrieveMultipleAsync(QueryExpression queryExpression);

    Task<T[]> RetrieveMultipleAsync<T>(QueryExpression queryExpression)
        where T : Entity;
}
