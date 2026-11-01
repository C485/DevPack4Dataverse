using Xunit;

namespace DevPack4Dataverse.Tests;

public class StringToSecureStringTests
{
    [Fact]
    public void ReturnsNull_WhenInputIsNull()
    {
        var result = SdkProxy.StringToSecureString(null);

        Assert.Null(result);
    }

    [Fact]
    public void ConvertsCharacters_AndMarksReadOnly()
    {
        const string value = "MySecret!123";

        var secure = SdkProxy.StringToSecureString(value);

        Assert.NotNull(secure);
        Assert.Equal(value.Length, secure.Length);
        Assert.True(secure.IsReadOnly());
    }
}
