namespace DevPack4Dataverse.Interfaces;

/// <summary>
/// A connection that can report when no request is using it. The proxy uses this during
/// disposal so a client is not closed under an in-flight request.
/// </summary>
internal interface IConnectionIdleWaiter
{
    Task WaitForIdleAsync(CancellationToken cancellationToken = default);
}
