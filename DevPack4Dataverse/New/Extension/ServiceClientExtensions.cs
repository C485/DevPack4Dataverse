using Microsoft.PowerPlatform.Dataverse.Client;

namespace DevPack4Dataverse.New.Extension;

public static class ServiceClientExtensions
{
    public static ServiceClientExtensionsInner Extension(this ServiceClient serviceClient)
    {
        return new ServiceClientExtensionsInner(serviceClient);
    }
}
