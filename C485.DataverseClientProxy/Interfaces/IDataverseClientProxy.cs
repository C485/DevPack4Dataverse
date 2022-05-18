using C485.DataverseClientProxy.Models;
using System.Threading.Tasks;

namespace C485.DataverseClientProxy.Interfaces
{
    public interface IDataverseClientProxy : IDataConnectionLayer
    {
        AdvancedExecuteMultipleRequestsStatistics AdvancedExecuteMultipleRequests(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, ExecuteMultipleRequestSettings executeMultipleRequestSettings);

        Task<AdvancedExecuteMultipleRequestsStatistics> AdvancedExecuteMultipleRequestsAsync(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, ExecuteMultipleRequestSettings executeMultipleRequestSettings);
    }
}