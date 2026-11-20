/*
Copyright 2022 Kamil Skoracki / C485@GitHub

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using DevPack4Dataverse.Mapper.Lock;

namespace DevPack4Dataverse.Mapper.Redis;

/// <summary>
/// Redis-backed entity lock using SET NX PX and a compare-and-delete release script.
/// </summary>
public sealed class RedisEntityLock : IEntityLock
{
    private const string ReleaseScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

    private const string RenewScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('pexpire', KEYS[1], ARGV[2]) else return 0 end";

    private readonly StackExchange.Redis.IDatabase _database;
    private readonly TimeSpan _lockTimeToLive;
    private readonly string _prefix;

    public RedisEntityLock(
        StackExchange.Redis.IConnectionMultiplexer connection,
        string keyPrefix = "devpack:mapper:lock:",
        TimeSpan? lockTimeToLive = null
    )
    {
        // Use a unique prefix per environment when several environments share one Redis.
        if (connection == null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        _database = connection.GetDatabase();
        _prefix = string.IsNullOrWhiteSpace(keyPrefix) ? "devpack:mapper:lock:" : keyPrefix;
        _lockTimeToLive = lockTimeToLive ?? TimeSpan.FromMinutes(5);
        if (_lockTimeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lockTimeToLive),
                "Lock time to live must be positive."
            );
        }
    }

    public async Task<IEntityLockHandle> AcquireAsync(
        EntityKey key,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Lock timeout must be positive.");
        }

        StackExchange.Redis.RedisKey redisKey = _prefix + key;
        string token = Guid.NewGuid().ToString("N");
        DateTime deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool acquired;
            try
            {
                acquired = await _database
                    .StringSetAsync(
                        redisKey,
                        token,
                        _lockTimeToLive,
                        StackExchange.Redis.When.NotExists
                    )
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new MapperLockException(
                    $"Redis lock acquisition failed for {key}.",
                    exception
                );
            }

            if (acquired)
            {
                return new RedisLockHandle(_database, redisKey, token, _lockTimeToLive);
            }

            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        }

        throw new MapperLockException($"Could not acquire a Redis lock for {key} within {timeout}.");
    }

    private sealed class RedisLockHandle : IEntityLockHandle
    {
        private readonly StackExchange.Redis.IDatabase _database;
        private readonly StackExchange.Redis.RedisKey _key;
        private readonly Timer _renewTimer;
        private readonly long _timeToLiveMilliseconds;
        private readonly string _token;
        private int _released;

        public RedisLockHandle(
            StackExchange.Redis.IDatabase database,
            StackExchange.Redis.RedisKey key,
            string token,
            TimeSpan timeToLive
        )
        {
            _database = database;
            _key = key;
            _token = token;
            _timeToLiveMilliseconds = Math.Max(1, (long)timeToLive.TotalMilliseconds);
            double renewMilliseconds = Math.Max(50, _timeToLiveMilliseconds / 3.0);
            RenewalState state = new(this);
            _renewTimer = new Timer(
                static renewalState => Renew(renewalState),
                state,
                TimeSpan.FromMilliseconds(renewMilliseconds),
                TimeSpan.FromMilliseconds(renewMilliseconds)
            );
            state.Timer = _renewTimer;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                return;
            }

            _renewTimer.Dispose();
            try
            {
                _database.ScriptEvaluate(
                    ReleaseScript,
                    new[] { _key },
                    new StackExchange.Redis.RedisValue[] { _token }
                );
            }
            catch (Exception)
            {
                // The lock expires on its own; a failed release must not mask a successful save.
            }
        }

        private void Renew()
        {
            if (Volatile.Read(ref _released) != 0)
            {
                return;
            }

            try
            {
                _database.ScriptEvaluate(
                    RenewScript,
                    new[] { _key },
                    new StackExchange.Redis.RedisValue[]
                    {
                        _token,
                        _timeToLiveMilliseconds
                    }
                );
            }
            catch (Exception)
            {
                // A failed renewal leaves the current TTL in place.
            }
        }

        private static void Renew(object? state)
        {
            RenewalState renewal = (RenewalState)state!;
            if (!renewal.Handle.TryGetTarget(out RedisLockHandle? handle))
            {
                // A leaked handle must not keep the lock alive forever.
                renewal.Timer?.Dispose();
                return;
            }

            handle.Renew();
        }

        private sealed class RenewalState(RedisLockHandle handle)
        {
            public WeakReference<RedisLockHandle> Handle { get; } = new(handle);
            public Timer? Timer { get; set; }
        }
    }
}
