using Xunit;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

public class ConnectionLeaseTests
{
    [Fact]
    public void Constructor_NullConnection_DoesNotCrashOnFinalization()
    {
        Assert.Throws<ArgumentNullException>(() => new ConnectionLease(null!));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [Fact]
    public void Dispose_ReleasesTheConnectionOnce()
    {
        var connection = new FakeConnection();
        var lease = new ConnectionLease(connection);

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(1, connection.ReleaseCount);
    }
}
