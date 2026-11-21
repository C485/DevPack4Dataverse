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

namespace DevPack4Dataverse.Mapper.Lock;

/// <summary>
/// Default process-local entity lock.
/// </summary>
/// <remarks>
/// Uses a fixed set of striped locks so memory stays bounded. Unrelated keys can share a stripe.
/// Use an external lock implementation when high concurrency across many keys is required.
/// </remarks>
public sealed class InMemoryEntityLock : IEntityLock
{
    private const int StripeCount = 256;
    private readonly SemaphoreSlim[] _stripes = new SemaphoreSlim[StripeCount];

    public InMemoryEntityLock()
    {
        for (int index = 0; index < _stripes.Length; index++)
        {
            _stripes[index] = new SemaphoreSlim(1, 1);
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

        SemaphoreSlim semaphore = _stripes[GetStripe(key)];
        bool acquired = await semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (!acquired)
        {
            throw new MapperLockException(
                $"Could not acquire a lock for {key} within {timeout}."
            );
        }

        return new LockHandle(semaphore);
    }

    private static int GetStripe(EntityKey key) =>
        (key.ToString().GetHashCode() & int.MaxValue) % StripeCount;

    private sealed class LockHandle(SemaphoreSlim semaphore) : IEntityLockHandle, IEntityLockState
    {
        private int _released;

        public bool IsLost => false;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}
