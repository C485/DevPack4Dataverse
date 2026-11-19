using Moq;
using StackExchange.Redis;
using Xunit;
using Microsoft.Xrm.Sdk;
using DevPack4Dataverse.Mapper;
using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Mapper.Redis;

namespace DevPack4Dataverse.Tests;

public class RedisEntityCacheTests
{
    [Fact]
    public async Task RemoveAsync_RemovesRecordIdIndexEntry()
    {
        Guid id = Guid.NewGuid();
        EntityKey key = EntityKey.ById("account", id);
        string cacheKey = "test:" + key;
        string indexKey = "test:id:account|" + id.ToString("N");
        string json = RedisEntityCache.Serialize(
            new CachedEntity(new Entity("account", id) { ["name"] = "Contoso" }, new[] { "name" })
        );

        var database = new Mock<IDatabase>();
        database
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(json);
        var connection = new Mock<IConnectionMultiplexer>();
        connection
            .Setup(redis => redis.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);

        var cache = new RedisEntityCache(connection.Object, "test:");
        await cache.RemoveAsync(key);

        database.Verify(
            db => db.SetRemoveAsync(indexKey, cacheKey, CommandFlags.None),
            Times.Once
        );
        database.Verify(db => db.KeyDeleteAsync(cacheKey, CommandFlags.None), Times.Once);
    }
}
