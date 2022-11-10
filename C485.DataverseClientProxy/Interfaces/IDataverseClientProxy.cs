using System.Threading;
using System.Threading.Tasks;
using C485.DataverseClientProxy.Models;

namespace C485.DataverseClientProxy.Interfaces;

public interface IDataverseClientProxy : IDataverseConnectionLayer
{
	//AdvancedExecuteMultipleRequestsStatistics AdvancedExecuteMultipleRequests(
	//	ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
	//	ExecuteMultipleRequestSettings executeMultipleRequestSettings,
	//	CancellationToken cancellationToken = default);

	//Task<AdvancedExecuteMultipleRequestsStatistics> AdvancedExecuteMultipleRequestsAsync(
	//	ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
	//	ExecuteMultipleRequestSettings executeMultipleRequestSettings,
	//	CancellationToken cancellationToken = default);
}