using DevPack4Dataverse.Models;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevPack4Dataverse.Tests;

public class CallerIdRestoreTests
{
    [Fact]
    public void Dispose_RestoresBothCallerIdValues()
    {
        Guid oldId = Guid.NewGuid();
        Guid? oldAad = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        Guid? targetAad = Guid.NewGuid();
        var client = new FakeCallerIdClient
        {
            CallerId = oldId,
            CallerAADObjectId = oldAad
        };

        using (
            new ReplaceAndRestoreCallerId(
                client,
                NullLogger.Instance,
                new RequestImpersonateSettings
                {
                    ImpersonateAsUserByDataverseId = targetId,
                    ImpersonateAsUserByAADId = targetAad
                }
            )
        )
        {
            Assert.Equal(targetId, client.CallerId);
            Assert.Equal(targetAad, client.CallerAADObjectId);
        }

        Assert.Equal(oldId, client.CallerId);
        Assert.Equal(oldAad, client.CallerAADObjectId);
    }

    [Fact]
    public void Dispose_RestoresCallerIdAfterAThrowingRequest()
    {
        Guid oldId = Guid.NewGuid();
        var client = new FakeCallerIdClient
        {
            CallerId = oldId
        };

        try
        {
            using (
                new ReplaceAndRestoreCallerId(
                    client,
                    NullLogger.Instance,
                    new RequestImpersonateSettings
                    {
                        ImpersonateAsUserByDataverseId = Guid.NewGuid()
                    }
                )
            )
            {
                throw new InvalidOperationException("request failed");
            }
        }
        catch (InvalidOperationException)
        {
        }

        Assert.Equal(oldId, client.CallerId);
    }

    [Fact]
    public void ConnectionWithoutSupport_ThrowsBeforeChangingCallerId()
    {
        Guid oldId = Guid.NewGuid();
        var client = new FakeCallerIdClient
        {
            CallerId = oldId,
            SupportsCallerId = false
        };

        Assert.Throws<NotSupportedException>(
            () =>
                new ReplaceAndRestoreCallerId(
                    client,
                    NullLogger.Instance,
                    new RequestImpersonateSettings
                    {
                        ImpersonateAsUserByDataverseId = Guid.NewGuid()
                    }
                )
        );

        Assert.Equal(oldId, client.CallerId);
    }

    [Fact]
    public void ClientThatIgnoresCallerId_RestoresBothOldValuesBeforeThrowing()
    {
        Guid oldId = Guid.NewGuid();
        Guid? oldAad = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        Guid targetAad = Guid.NewGuid();
        var client = new FakeCallerIdClient
        {
            CallerId = oldId,
            CallerAADObjectId = oldAad,
            IgnoreCallerAad = true
        };

        Assert.Throws<NotSupportedException>(
            () =>
                new ReplaceAndRestoreCallerId(
                    client,
                    NullLogger.Instance,
                    new RequestImpersonateSettings
                    {
                        ImpersonateAsUserByDataverseId = targetId,
                        ImpersonateAsUserByAADId = targetAad
                    }
                )
        );

        Assert.Equal(oldId, client.CallerId);
        Assert.Equal(oldAad, client.CallerAADObjectId);
    }

    [Fact]
    public void ClientThatIgnoresCallerId_ThrowsInsteadOfRunningAsTheWrongUser()
    {
        var client = new FakeCallerIdClient
        {
            IgnoreCallerId = true
        };

        Assert.Throws<NotSupportedException>(
            () =>
                new ReplaceAndRestoreCallerId(
                    client,
                    NullLogger.Instance,
                    new RequestImpersonateSettings
                    {
                        ImpersonateAsUserByDataverseId = Guid.NewGuid()
                    }
                )
        );
    }

    private sealed class FakeCallerIdClient : ICallerIdClient
    {
        private Guid _callerId;
        private Guid? _callerAadObjectId;

        public bool SupportsCallerId { get; set; } = true;
        public bool IgnoreCallerId { get; set; }
        public bool IgnoreCallerAad { get; set; }

        public Guid CallerId
        {
            get => _callerId;
            set
            {
                if (!IgnoreCallerId)
                {
                    _callerId = value;
                }
            }
        }

        public Guid? CallerAADObjectId
        {
            get => _callerAadObjectId;
            set
            {
                if (!IgnoreCallerAad)
                {
                    _callerAadObjectId = value;
                }
            }
        }
    }
}
