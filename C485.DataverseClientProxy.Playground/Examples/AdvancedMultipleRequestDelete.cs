using C485.DataverseClientProxy.Creators;
using C485.DataverseClientProxy.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace C485.DataverseClientProxy.Playground.Examples
{
    public class AdvancedMultipleRequestDelete : IExecute
    {
        private const string EntityName = "crf87_test";

        //You'll need at least 4 accounts/application accounts
        public async Task Execute()
        {
            DataverseClientProxy connectionManager = new();
            Task.WaitAll(
                 connectionManager.AddNewConnection(new ClientSecretConnectionCreator("", "", CrmServiceClient.MakeSecureString(""))),
                 connectionManager.AddNewConnection(new ClientSecretConnectionCreator("", "", CrmServiceClient.MakeSecureString(""))),
                 connectionManager.AddNewConnection(new ClientSecretConnectionCreator("", "", CrmServiceClient.MakeSecureString(""))),
                 connectionManager.AddNewConnection(new ClientSecretConnectionCreator("", "", CrmServiceClient.MakeSecureString(""))),
                 connectionManager.AddNewConnection(new ClientSecretConnectionCreator("", "", CrmServiceClient.MakeSecureString(""))),
                 connectionManager.AddNewConnection(new ClientSecretConnectionCreator("", "", CrmServiceClient.MakeSecureString(""))),
                 connectionManager.AddNewConnection(new ClientSecretConnectionCreator("", "", CrmServiceClient.MakeSecureString(""))),
                 connectionManager.AddNewConnection(new ClientSecretConnectionCreator("", "", CrmServiceClient.MakeSecureString(""))),
                 connectionManager.AddNewConnection(new ClientSecretConnectionCreator("", "", CrmServiceClient.MakeSecureString(""))),
                 connectionManager.AddNewConnection(new ClientSecretConnectionCreator("", "", CrmServiceClient.MakeSecureString(""))),
                 connectionManager.AddNewConnection(new ClientSecretConnectionCreator("", "", CrmServiceClient.MakeSecureString("")))
                );

            IEnumerable<Entity> recordsToDelete = await RetriveAllRecordsToDelete();

            ExecuteMultipleRequestBuilder executeMultipleRequestBuilder = new(true, true);

            foreach (Entity item in recordsToDelete)
            {
                //Trim to remove reference and create copy
                executeMultipleRequestBuilder.AddDelete(new EntityReference(item.LogicalName.Trim(), item.Id));
            }

            AdvancedExecuteMultipleRequestsStatistics executeStatistic = await connectionManager.AdvancedExecuteMultipleRequestsAsync(executeMultipleRequestBuilder,
                new ExecuteMultipleRequestSettings
                {
                    ErrorReport = (OrganizationRequest obj, string error) => Console.WriteLine($"Error: {error}"),
                    ReportProgress = (int cur, int max) => Console.Title = $"Progress delete[{cur}/{max}]",
                    ReportProgressInterval = TimeSpan.FromMilliseconds(100)
                });
        }

        /// <summary>
        /// You can use connection alone without connection manager, or you can write your own. Just
        /// remember to call DiableLockingCheck or use ConnectionLease class with using.
        /// </summary>
        /// <returns></returns>
        private async Task<IEnumerable<Entity>> RetriveAllRecordsToDelete()
        {
            ClientSecretConnectionCreator connectionCreator1 = new ClientSecretConnectionCreator("", "", CrmServiceClient.MakeSecureString(""));
            Interfaces.IConnection tmpConnection = connectionCreator1.Create();
            tmpConnection.DiableLockingCheck();
            return await tmpConnection.RetriveMultipleAsync(new QueryExpression
            {
                EntityName = EntityName,
                ColumnSet = new ColumnSet(false),
            });
        }
    }
}