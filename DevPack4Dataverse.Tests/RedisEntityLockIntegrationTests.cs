using DevPack4Dataverse.Mapper;
using DevPack4Dataverse.Mapper.Lock;
using DevPack4Dataverse.Mapper.Redis;
using StackExchange.Redis;
using Xunit;

namespace DevPack4Dataverse.Tests;

public sealed class RedisEntityLockIntegrationTests
{
    [RedisFact]
    public async Task Acquire_BlocksSecondOwner_AndReleaseAllowsReacquire()
    {
        using ConnectionMultiplexer connection = await RedisIntegrationTest.ConnectAsync();
        using ConnectionMultiplexer secondConnection = await RedisIntegrationTest.ConnectAsync();
        string prefix = RedisIntegrationTest.NewPrefix();
        RedisEntityLock first = new(connection, prefix);
        RedisEntityLock second = new(secondConnection, prefix);
        EntityKey key = EntityKey.ById("account", Guid.NewGuid());

        using (IEntityLockHandle handle = await first.AcquireAsync(key, TimeSpan.FromSeconds(1)))
        {
            Assert.NotNull(handle);
            await Assert.ThrowsAsync<MapperLockException>(
                () => second.AcquireAsync(key, TimeSpan.FromMilliseconds(300))
            );
        }

        using IEntityLockHandle reacquired = await second.AcquireAsync(
            key,
            TimeSpan.FromSeconds(1)
        );
        Assert.NotNull(reacquired);
    }

    [RedisFact]
    public async Task DifferentKeys_DoNotBlockEachOther()
    {
        using ConnectionMultiplexer connection = await RedisIntegrationTest.ConnectAsync();
        RedisEntityLock entityLock = new(connection, RedisIntegrationTest.NewPrefix());

        using IEntityLockHandle first = await entityLock.AcquireAsync(
            EntityKey.ById("account", Guid.NewGuid()),
            TimeSpan.FromSeconds(1)
        );
        using IEntityLockHandle second = await entityLock.AcquireAsync(
            EntityKey.ById("account", Guid.NewGuid()),
            TimeSpan.FromSeconds(1)
        );

        Assert.NotNull(first);
        Assert.NotNull(second);
    }

    [RedisFact]
    public async Task LockExpires_AfterTimeToLive()
    {
        using ConnectionMultiplexer connection = await RedisIntegrationTest.ConnectAsync();
        string prefix = RedisIntegrationTest.NewPrefix();
        RedisEntityLock entityLock = new(connection, prefix, TimeSpan.FromSeconds(1));
        EntityKey key = EntityKey.ById("account", Guid.NewGuid());
        string redisKey = prefix + key;
        IDatabase database = connection.GetDatabase();

        IEntityLockHandle handle = await entityLock.AcquireAsync(key, TimeSpan.FromSeconds(1));
        try
        {
            Assert.True(await database.KeyExistsAsync(redisKey));
            TimeSpan? ttl = await database.KeyTimeToLiveAsync(redisKey);
            Assert.True(ttl.HasValue && ttl.Value > TimeSpan.Zero);

            await Task.Delay(1500);
            Assert.False(await database.KeyExistsAsync(redisKey));
        }
        finally
        {
            handle.Dispose();
        }

        using IEntityLockHandle reacquired = await entityLock.AcquireAsync(
            key,
            TimeSpan.FromSeconds(1)
        );
        Assert.NotNull(reacquired);
    }

    [RedisFact]
    public async Task Release_DoesNotDeleteAnotherOwnersLock()
    {
        using ConnectionMultiplexer connection = await RedisIntegrationTest.ConnectAsync();
        string prefix = RedisIntegrationTest.NewPrefix();
        RedisEntityLock entityLock = new(connection, prefix);
        EntityKey key = EntityKey.ById("account", Guid.NewGuid());
        string redisKey = prefix + key;
        IDatabase database = connection.GetDatabase();

        IEntityLockHandle handle = await entityLock.AcquireAsync(key, TimeSpan.FromSeconds(1));
        await database.StringSetAsync(redisKey, "another-owner", TimeSpan.FromMinutes(1));

        handle.Dispose();

        Assert.True(await database.KeyExistsAsync(redisKey));
        await database.KeyDeleteAsync(redisKey);
    }

    [RedisFact]
    public async Task Acquire_HonorsCancellation()
    {
        using ConnectionMultiplexer connection = await RedisIntegrationTest.ConnectAsync();
        RedisEntityLock entityLock = new(connection, RedisIntegrationTest.NewPrefix());
        EntityKey key = EntityKey.ById("account", Guid.NewGuid());

        using IEntityLockHandle handle = await entityLock.AcquireAsync(
            key,
            TimeSpan.FromSeconds(1)
        );
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => entityLock.AcquireAsync(key, TimeSpan.FromSeconds(5), cancellation.Token)
        );
    }

    [RedisFact]
    public async Task InvalidArguments_Throw()
    {
        using ConnectionMultiplexer connection = await RedisIntegrationTest.ConnectAsync();
        RedisEntityLock entityLock = new(connection, RedisIntegrationTest.NewPrefix());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => entityLock.AcquireAsync(null!, TimeSpan.FromSeconds(1))
        );
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => entityLock.AcquireAsync(EntityKey.ById("account", Guid.NewGuid()), TimeSpan.Zero)
        );
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RedisEntityLock(connection, RedisIntegrationTest.NewPrefix(), TimeSpan.Zero)
        );
    }
}
