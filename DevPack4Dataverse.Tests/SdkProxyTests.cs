using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

public class SdkProxyTests
{
    [Fact]
    public void GetConnection_ReturnsLeasedConnection()
    {
        var fake = new FakeConnection();
        var proxy = new SdkProxy(NullLogger.Instance, false, new FakeConnectionCreator(fake));

        using var lease = proxy.GetConnection();

        Assert.Same(fake, lease.Connection);
    }

    [Fact]
    public void GetConnection_WithoutCreators_Throws()
    {
        var proxy = new SdkProxy(NullLogger.Instance, false);

        Assert.ThrowsAny<ArgumentException>(() => proxy.GetConnection());
    }
}
