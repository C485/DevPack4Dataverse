using Microsoft.Crm.Sdk.Messages;

namespace C485.DataverseClientProxy.Interfaces;

public interface IConnectionCreator
{
    /// <summary>
    ///
    /// </summary>
    bool IsCreated { get; }

    /// <summary>
    ///
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    ///  <para>
    ///   In this method class which implements this interface should create
    ///   <see
    ///    cref="Connection" />
    ///   object.
    ///  </para>
    ///  <para>
    ///   Class should also check if connection is active, while adding connection
    ///   <see
    ///    cref="WhoAmIRequest" />
    ///   is executed to finally confirm that connection was established.
    ///  </para>
    /// </summary>
    /// <returns><see cref="IConnection" /> as instance of <see cref="Connection" /></returns>
    IConnection Create();
}