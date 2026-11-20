using Xunit;
using DevPack4Dataverse.Creators;

namespace DevPack4Dataverse.Tests;

public class ConnectionStringConnectionCreatorTests
{
    [Fact]
    public void AppendsRequireNewInstance_WhenMissing()
    {
        var creator = new ConnectionStringConnectionCreator("AuthType=ClientSecret;Url=https://example.crm.dynamics.com");

        Assert.Contains(
            "RequireNewInstance=True",
            creator.ConnectionString,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.False(creator.IsCreated);
        Assert.False(creator.IsError);
        Assert.False(creator.IsValid);
    }

    [Fact]
    public void AcceptsRequireNewInstanceWithSpaces()
    {
        var creator = new ConnectionStringConnectionCreator(
            "AuthType=ClientSecret;Url=https://example.crm.dynamics.com;RequireNewInstance = True");

        Assert.NotNull(creator);
    }

    [Fact]
    public void RejectsInvalidRequireNewInstanceValue()
    {
        var exception = Record.Exception(() =>
            new ConnectionStringConnectionCreator(
                "AuthType=ClientSecret;Url=https://example.crm.dynamics.com;RequireNewInstance=TrueX"));

        Assert.IsType<InvalidProgramException>(exception);
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
    public void RejectsWhitespaceConnectionString()
    {
        var exception = Record.Exception(() => new ConnectionStringConnectionCreator("   "));

        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void ValidatesConnectionCount_WhenStringDoesNotEndWithSemicolon()
    {
        var exception = Record.Exception(() =>
            new ConnectionStringConnectionCreator("AuthType=ClientSecret;Url=https://example.crm.dynamics.com", 0));

        Assert.NotNull(exception);
    }
}
