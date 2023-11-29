using Ardalis.GuardClauses;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace DevPack4Dataverse.Extension
{
    public static class ServiceClientExtension
    {
        public static ServiceClient ApplyConnectionOptimization(this ServiceClient serviceClient)
        {
            Guard.Against.Null(serviceClient).EnableAffinityCookie = false;
            return serviceClient;
        }
    }
}
