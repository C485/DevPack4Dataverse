using C485.DataverseClientProxy.Models;

namespace C485.DataverseClientProxy.Interfaces
{
    public interface IConnectionManager : IDataConnectionLayer
    {
        ChunksStatistics ExecuteMultipleAsChunks(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, ExecuteMultipleRequestSettings executeMultipleRequestSettings);
    }
}