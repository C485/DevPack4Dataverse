using StackExchange.Redis;
using Xunit;

namespace DevPack4Dataverse.Tests;

internal sealed class RedisFactAttribute : FactAttribute
{
    internal const string ConnectionVariable = "DEVPACK_REDIS_CONNECTION";

    public RedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
        {
            Skip = $"Set {ConnectionVariable} to a Redis connection string (for example 127.0.0.1:6390,allowAdmin=true) to run this test.";
        }
    }
}

internal static class RedisIntegrationTest
{
    internal static Task<ConnectionMultiplexer> ConnectAsync() =>
        ConnectionMultiplexer.ConnectAsync(
            Environment.GetEnvironmentVariable(RedisFactAttribute.ConnectionVariable)!
        );

    internal static string NewPrefix() => $"devpack:test:{Guid.NewGuid():N}:";
}
