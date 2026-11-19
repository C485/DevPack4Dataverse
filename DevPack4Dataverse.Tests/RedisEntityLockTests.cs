using Moq;
using StackExchange.Redis;
using Xunit;
using DevPack4Dataverse.Mapper;
using DevPack4Dataverse.Mapper.Lock;
using DevPack4Dataverse.Mapper.Redis;

namespace DevPack4Dataverse.Tests;

public class RedisEntityLockTests
{
    [Fact]
    public async Task AcquireAndRelease_UsesRedisLockScript()
    {
        var database = new Mock<IDatabase>();
        database
            .Setup(
                db =>
                    db.StringSetAsync(
                        It.IsAny<RedisKey>(),
                        It.IsAny<RedisValue>(),
                        It.IsAny<TimeSpan?>(),
                        When.NotExists
                    )
            )
            .ReturnsAsync(true);

        var connection = new Mock<IConnectionMultiplexer>();
        connection
            .Setup(redis => redis.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);

        var entityLock = new RedisEntityLock(connection.Object);
        using (IEntityLockHandle handle = await entityLock.AcquireAsync(
                   EntityKey.ById("account", Guid.NewGuid()),
                   TimeSpan.FromSeconds(1)
               ))
        {
            Assert.NotNull(handle);
        }

        database.Verify(
            db =>
                db.ScriptEvaluate(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    CommandFlags.None
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task AcquireWhenSetFails_ThrowsLockException()
    {
        var database = new Mock<IDatabase>();
        database
            .Setup(
                db =>
                    db.StringSetAsync(
                        It.IsAny<RedisKey>(),
                        It.IsAny<RedisValue>(),
                        It.IsAny<TimeSpan?>(),
                        When.NotExists
                    )
            )
            .ReturnsAsync(false);

        var connection = new Mock<IConnectionMultiplexer>();
        connection
            .Setup(redis => redis.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);

        var entityLock = new RedisEntityLock(connection.Object);

        await Assert.ThrowsAsync<MapperLockException>(
            () =>
                entityLock.AcquireAsync(
                    EntityKey.ById("account", Guid.NewGuid()),
                    TimeSpan.FromMilliseconds(50)
                )
        );
    }

    [Fact]
    public async Task AcquireRetriesWhenTheKeyIsTemporarilyHeld()
    {
        var database = new Mock<IDatabase>();
        database
            .SetupSequence(
                db =>
                    db.StringSetAsync(
                        It.IsAny<RedisKey>(),
                        It.IsAny<RedisValue>(),
                        It.IsAny<TimeSpan?>(),
                        When.NotExists
                    )
            )
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        var connection = new Mock<IConnectionMultiplexer>();
        connection
            .Setup(redis => redis.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);

        var entityLock = new RedisEntityLock(connection.Object);
        using IEntityLockHandle handle = await entityLock.AcquireAsync(
            EntityKey.ById("account", Guid.NewGuid()),
            TimeSpan.FromSeconds(1)
        );

        Assert.NotNull(handle);
        database.Verify(
            db =>
                db.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists
                ),
            Times.Exactly(2)
        );
    }
}
