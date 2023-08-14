using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace DevPack4Dataverse.New;

public interface IConnectionCreator
{
    /// <summary>
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    ///     <para>
    ///         This method creates
    ///         <see
    ///             cref="ServiceClient" />
    ///         .
    ///     </para>
    /// </summary>
    /// <returns>
    ///     <see cref="ServiceClient" />
    /// </returns>
    ServiceClient Create(ILogger logger);
}

public class ConnectionBuilder
{
    private readonly ILogger _logger;

    public ConnectionBuilder(ILogger logger)
    {
        Guard.IsNotNull(logger);
        _logger = logger;
    }

    public ServiceClient Create(IConnectionCreator connectionCreator)
    {
        Guard.IsTrue(connectionCreator.IsValid);

        return connectionCreator.Create(_logger);
    }
}
