using System.Threading.Tasks;

namespace C485.DataverseClientProxy.Interfaces
{
    public interface IConnection : IDataConnectionLayer
    {
        void DiableLockingCheck();

        bool IsLockedByThisThread();

        void ReleaseLock();

        bool Test();

        Task<bool> TestAsync();

        bool TryLock();
    }
}