using Ardalis.GuardClauses;
using C485.DataverseClientProxy.Models;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace C485.DataverseClientProxy
{
    public class ReplaceAndRestoreCallerId : IDisposable
    {
        private readonly Guid? oldAADCallerId;
        private readonly Guid? oldCallerId;
        private bool _disposedValue;

        public ReplaceAndRestoreCallerId(ServiceClient serviceClient, Guid? callerId = null, Guid? aadCallerId = null)
        {
            ServiceClient = Guard
                .Against
                .Null(serviceClient);
            oldCallerId = serviceClient.CallerId;
            oldAADCallerId = serviceClient.CallerAADObjectId;
            serviceClient.CallerId = callerId ?? Guid.Empty;
            serviceClient.CallerAADObjectId = aadCallerId;
        }

        public ReplaceAndRestoreCallerId(ServiceClient serviceClient, RequestSettings requestSettings = null)
        {
            ServiceClient = Guard
                .Against
                .Null(serviceClient);
            oldCallerId = serviceClient.CallerId;
            oldAADCallerId = serviceClient.CallerAADObjectId;
            serviceClient.CallerAADObjectId = requestSettings.ImpersonateAsUserByAADId;
            serviceClient.CallerId = requestSettings.ImpersonateAsUserByDataverseId ?? Guid.Empty;
        }

        ~ReplaceAndRestoreCallerId()
        {
            Dispose(false);
        }

        private ServiceClient ServiceClient { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue)
            {
                return;
            }

            ServiceClient.CallerId = oldCallerId ?? Guid.Empty;
            ServiceClient.CallerAADObjectId = oldAADCallerId;
            ServiceClient = null;
            _disposedValue = true;
        }
    }
}