
using C485.DataverseClientProxy.Creators;
using C485.DataverseClientProxy.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace C485.DataverseClientProxy.Playground.Examples
{
    public class AdvancedMultipleRequest : IExecute
    {
        private const string EntityName = "crf87_test";
        private const string EntityNameFieldName = "crf87_name";
        private const int RecordsToCreate = 10000;

        //You'll need at least 4 accounts/application accounts
        //Make sure they've ExecuteMultiple
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

            ExecuteMultipleRequestBuilder executeMultipleRequestBuilder = new(true, true);
            foreach (int item in Enumerable.Range(0, RecordsToCreate))
            {
                Entity et = new(EntityName);
                et[EntityNameFieldName] = $"Record [{item}]";
                executeMultipleRequestBuilder.AddCreate(et);
            }
            AdvancedExecuteMultipleRequestsStatistics executeStatistic = await connectionManager.AdvancedExecuteMultipleRequestsAsync(executeMultipleRequestBuilder,
                new ExecuteMultipleRequestSettings
                {
                    ErrorReport = (OrganizationRequest obj, string error) => Console.WriteLine($"Error: {error}"),
                    ReportProgress = (int cur, int max) => Console.Title = $"Progress[{cur}/{max}]",
                    ReportProgressInterval = TimeSpan.FromMilliseconds(100)
                });
        }
    }
}