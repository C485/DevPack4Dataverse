using Ardalis.GuardClauses;
using C485.DataverseClientProxy.Interfaces;
using Microsoft.Xrm.Tooling.Connector;
using System;
using System.Linq;

namespace C485.DataverseClientProxy.Creators
{
    public class ConnectionStringConnectionCreator : IConnectionCreator
    {
        private const string ConnectionStringInvalidError = "Unable to append RequireNewInstance because other option was provided. 'RequireNewInstance=True' is requred to be properly detected.";
        private readonly string _connectionString;

        public ConnectionStringConnectionCreator(string connectionString)
        {
            _connectionString = Guard
                .Against
                .NullOrEmpty(connectionString, nameof(connectionString));

            if (_connectionString.IndexOf("RequireNewInstance=True", StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                return;
            }
            else if (_connectionString.IndexOf("RequireNewInstance", StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                throw new InvalidProgramException(ConnectionStringInvalidError);
            }

            _connectionString = _connectionString.Trim();

            if (_connectionString.Last() == ';')
            {
                _connectionString += "RequireNewInstance=True";
                return;
            }

            _connectionString += ";RequireNewInstance=True";
        }

        public IConnection Create()
        {
            CrmServiceClient crmServiceClient = new(_connectionString);
            Guard
                .Against
                .NullOrInvalidInput(crmServiceClient,
                    nameof(crmServiceClient),
                    p => p.IsReady,
                    $"{nameof(ClientSecretConnectionCreator)} - failed to make connection to connection string, LatestError: {crmServiceClient?.LastCrmError}");
            return new Connection(crmServiceClient);
        }
    }
}