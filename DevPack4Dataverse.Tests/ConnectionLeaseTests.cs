using System.Runtime.CompilerServices;
using Xunit;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

public class ConnectionLeaseTests
{
    [Fact]
    public void Constructor_NullConnection_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ConnectionLease(null!));
    }

    [Fact]
    public void LeakedLease_IsNotReleasedByTheFinalizer()
    {
        var connection = new FakeConnection();
        CreateLeakedLease(connection);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.Equal(0, connection.ReleaseCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateLeakedLease(FakeConnection connection)
    {
        _ = new ConnectionLease(connection);
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
