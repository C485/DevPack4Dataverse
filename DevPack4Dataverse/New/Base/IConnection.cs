using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.New.Base;

public interface IConnection
{
    bool Test();

    Entity RefreshRecord(Entity record);

    T RefreshRecord<T>(T record) where T : Entity;

    Entity Retrieve(
        string logicalName,
        Guid id,
        ColumnSet columnSet);

    T Retrieve<T>(
        string logicalName,
        Guid id,
        ColumnSet columnSet)
        where T : Entity;

    T Execute<T>(OrganizationRequest request) where T : OrganizationResponse;

    ExecuteMultipleResponse Execute(ExecuteMultipleRequest executeMultipleRequest);

    Entity[] RetrieveMultiple(QueryExpression queryExpression);

    T[] RetrieveMultiple<T>(QueryExpression queryExpression) where T : Entity;
}
