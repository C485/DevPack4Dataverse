using Xunit;
using DevPack4Dataverse.Creators;

namespace DevPack4Dataverse.Tests;

public class ConnectionStringConnectionCreatorTests
{
    [Fact]
    public void AppendsRequireNewInstance_WhenMissing()
    {
        var creator = new ConnectionStringConnectionCreator("AuthType=ClientSecret;Url=https://example.crm.dynamics.com");

        Assert.False(creator.IsCreated);
        Assert.False(creator.IsError);
        Assert.False(creator.IsValid);
    }

    [Fact]
    public void AcceptsExistingRequireNewInstance()
    {
        var creator = new ConnectionStringConnectionCreator(
            "AuthType=ClientSecret;Url=https://example.crm.dynamics.com;RequireNewInstance=True");

        Assert.NotNull(creator);
    }

    [Fact]
    public void RejectsConflictingRequireNewInstanceValue()
    {
        var exception = Record.Exception(() =>
            new ConnectionStringConnectionCreator(
                "AuthType=ClientSecret;Url=https://example.crm.dynamics.com;RequireNewInstance=False"));

        Assert.IsType<InvalidProgramException>(exception);
    }

    [Fact]
    public void RejectsEmptyConnectionString()
    {
        var exception = Record.Exception(() => new ConnectionStringConnectionCreator(""));

        Assert.NotNull(exception);
    }

    [Fact]
    public void ValidatesMaximumConcurrentUsage_WhenStringDoesNotEndWithSemicolon()
    {
        var exception = Record.Exception(() =>
            new ConnectionStringConnectionCreator("AuthType=ClientSecret;Url=https://example.crm.dynamics.com", -1));

        Assert.NotNull(exception);
    }
}
