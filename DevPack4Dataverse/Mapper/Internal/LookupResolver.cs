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
using DevPack4Dataverse.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace DevPack4Dataverse.Mapper.Internal;

internal sealed class LookupResolver
{
    private readonly IEntityLock _entityLock;
    private readonly RecordStore _recordStore;
    private readonly SdkProxy _sdkProxy;

    public LookupResolver(
        SdkProxy sdkProxy,
        IEntityLock entityLock,
        RecordStore recordStore
    )
    {
        _sdkProxy = sdkProxy ?? throw new ArgumentNullException(nameof(sdkProxy));
        _entityLock = entityLock ?? throw new ArgumentNullException(nameof(entityLock));
        _recordStore = recordStore ?? throw new ArgumentNullException(nameof(recordStore));
    }

    public async Task<EntityReference> ResolveAsync(
        EntityKey key,
        LookupMappingOptions options,
        Func<Entity> createFactory,
        MappingExecutionState state,
        CancellationToken cancellationToken
    )
    {
        if (key == null || key.IsEmpty)
        {
            throw new MapperException("Lookup key is empty.");
        }

        if (
            options.RetrievalMode == LookupRetrievalMode.RetrieveAllRecords
            && key.IsAlternateKey
            && options.AlternateKeyAttributes.Count == 0
        )
        {
            throw new MapperConfigurationException(
                $"Lookup '{key}' uses RetrieveAllRecords with an alternate key but does not declare AlternateKeyAttributes."
            );
        }

        bool forceRefresh = state.ForceRefresh;
        if (options.RetrievalMode == LookupRetrievalMode.RetrieveAllRecords)
        {
            await EnsureAllLoadedAsync(key.LogicalName, options, state, cancellationToken)
                .ConfigureAwait(false);
            forceRefresh = false;
        }

        Entity record = await _recordStore
            .GetAsync(
                key,
                options.Fields,
                options.UseCache,
                forceRefresh,
                state.RequestSettings,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (record == null && options.CreateIfNotExists)
        {
            using IEntityLockHandle lockHandle = await _entityLock
                .AcquireAsync(key, state.LockTimeout, cancellationToken)
                .ConfigureAwait(false);

            record = await _recordStore
                .GetAsync(
                    key,
                    options.Fields,
                    options.UseCache,
                    state.ForceRefresh,
                    state.RequestSettings,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (record == null)
            {
                if (createFactory == null)
                {
                    throw new MapperConfigurationException(
                        $"Lookup '{key}' has CreateIfNotExists enabled but no create factory."
                    );
                }

                record = await CreateAsync(key, createFactory, state, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (record == null)
        {
            if (options.ThrowIfNotFound)
            {
                throw new MapperNotFoundException($"Lookup record '{key}' was not found.");
            }

            return null;
        }

        return record.Id == Guid.Empty
            ? new EntityReference(record.LogicalName)
            : new EntityReference(record.LogicalName, record.Id);
    }

    private async Task EnsureAllLoadedAsync(
        string logicalName,
        LookupMappingOptions options,
        MappingExecutionState state,
        CancellationToken cancellationToken
    )
    {
        if (state.Preloads.TryGetValue(logicalName, out Task existing))
        {
            await existing.ConfigureAwait(false);
            return;
        }

        Task load = _recordStore.LoadAllAsync(
            logicalName,
            options.Fields,
            options.AlternateKeyAttributes,
            state.ForceRefresh,
            state.RequestSettings,
            cancellationToken
        );

        state.Preloads.TryAdd(logicalName, load);
        await load.ConfigureAwait(false);
    }

    private async Task<Entity> CreateAsync(
        EntityKey key,
        Func<Entity> createFactory,
        MappingExecutionState state,
        CancellationToken cancellationToken
    )
    {
        Entity entity = createFactory();
        if (entity == null)
        {
            throw new MapperException($"Create factory for lookup '{key}' returned null.");
        }

        if (string.IsNullOrWhiteSpace(entity.LogicalName))
        {
            entity.LogicalName = key.LogicalName;
        }
        else if (
            !string.Equals(entity.LogicalName, key.LogicalName, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new MapperConfigurationException(
                $"Create factory for lookup '{key}' returned entity '{entity.LogicalName}'."
            );
        }

        if (key.IsAlternateKey)
        {
            entity.Id = Guid.Empty;
            foreach (KeyValuePair<string, object> pair in key.AlternateKey)
            {
                entity.KeyAttributes[pair.Key] = pair.Value;
            }

            UpsertResponse response = await _sdkProxy
                .ExecuteAsync<UpsertResponse>(
                    new UpsertRequest { Target = entity },
                    state.RequestSettings
                )
                .ConfigureAwait(false);

            if (response?.Target == null)
            {
                throw new MapperException($"Dataverse did not return a reference for created lookup '{key}'.");
            }

            entity.Id = response.Target.Id;
        }
        else
        {
            // A non-alternate EntityKey always has a non-empty id.
            entity.Id = key.Id!.Value;
            _ = await _sdkProxy
                .ExecuteAsync<CreateResponse>(
                    new CreateRequest { Target = entity },
                    state.RequestSettings
                )
                .ConfigureAwait(false);
        }

        await _recordStore.RemoveRecordAsync(entity, cancellationToken).ConfigureAwait(false);
        await _recordStore.CacheSavedAsync(entity, cancellationToken).ConfigureAwait(false);
        return entity;
    }
}
