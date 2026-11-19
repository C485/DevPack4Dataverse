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

using System.Collections.Concurrent;

namespace DevPack4Dataverse.Mapper.Lock;

/// <summary>
/// Default process-local entity lock.
/// </summary>
public sealed class InMemoryEntityLock : IEntityLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks =
        new(StringComparer.Ordinal);

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

        SemaphoreSlim semaphore = _locks.GetOrAdd(key.ToString(), _ => new SemaphoreSlim(1, 1));
        bool acquired = await semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (!acquired)
        {
            throw new MapperLockException(
                $"Could not acquire a lock for {key} within {timeout}."
            );
        }

        return new LockHandle(semaphore);
    }

    private sealed class LockHandle(SemaphoreSlim semaphore) : IEntityLockHandle
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}
