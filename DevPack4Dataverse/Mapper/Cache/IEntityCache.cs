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

namespace DevPack4Dataverse.Mapper.Cache;

/// <summary>
/// Stores mapped records between executions. The default implementation is in-memory;
/// an external cache such as Redis can be injected through <see cref="MapperWrapper"/>.
/// </summary>
public interface IEntityCache
{
    Task<CachedEntity> GetAsync(EntityKey key, CancellationToken cancellationToken = default);

    Task SetAsync(
        EntityKey key,
        CachedEntity entry,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default
    );

    Task RemoveAsync(EntityKey key, CancellationToken cancellationToken = default);

    Task ClearAsync(string logicalName, CancellationToken cancellationToken = default);
}
