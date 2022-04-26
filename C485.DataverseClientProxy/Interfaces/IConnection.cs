using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Threading.Tasks;

namespace C485.DataverseClientProxy.Interfaces
{
    public interface IConnection : IDataConnectionLayer
    {
        void DiableLockingCheck();

        void ReleaseLock();

        Entity[] RetriveMultiple(QueryExpression queryExpression);

        bool Test();

        Task<bool> TestAsync();

        bool TryLock();
    }
}