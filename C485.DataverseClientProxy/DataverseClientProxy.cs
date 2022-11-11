using C485.DataverseClientProxy.Interfaces;
using C485.DataverseClientProxy.Logic;
using C485.DataverseClientProxy.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace C485.DataverseClientProxy;

public sealed class DataverseClientProxy : IDataverseClientProxy
{
    public readonly ConnectionManager ConnectionManager;
    public readonly ExecuteMultipleLogic ExecuteMultipleLogic;

    public DataverseClientProxy(params IConnectionCreator[] connectionCreators)
    {
        ConnectionManager = new ConnectionManager(connectionCreators);
        ExecuteMultipleLogic = new ExecuteMultipleLogic(ConnectionManager);
    }   
}