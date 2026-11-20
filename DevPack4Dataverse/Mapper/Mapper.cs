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

using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Mapper.Internal;
using DevPack4Dataverse.Mapper.Lock;
using DevPack4Dataverse.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace DevPack4Dataverse.Mapper;

/// <summary>
/// Immutable mapper definition. Safe to reuse for concurrent executions.
/// </summary>
public sealed class Mapper<TSource, TTarget> : IMapper
    where TTarget : class, new()
{
    private readonly IModelEntityConverter<TTarget> _converter;
    private readonly Func<MappingContext<TSource, TTarget>, EntityKey> _entityKeyFactory;
    private readonly IEntityLock _entityLock;
    private readonly string _logicalName;
    private readonly MapperOptions<TSource, TTarget> _options;
    private readonly IReadOnlyList<IMappingStep<TSource, TTarget>> _steps;
    private readonly RecordStore _recordStore;
    private readonly SdkProxy _sdkProxy;
    private readonly ValidationReader _validationReader;
    private readonly MapperWrapperOptions _wrapperOptions;

    internal Mapper(
        string name,
        SdkProxy sdkProxy,
        IReadOnlyList<IMappingStep<TSource, TTarget>> steps,
        MapperOptions<TSource, TTarget> options,
        IModelEntityConverter<TTarget> converter,
        Func<MappingContext<TSource, TTarget>, EntityKey> entityKeyFactory,
        string logicalName,
        RecordStore recordStore,
        ValidationReader validationReader,
        MapperWrapperOptions wrapperOptions,
        IEntityLock entityLock
    )
    {
        Name = name;
        _sdkProxy = sdkProxy;
        _steps = steps;
        _options = options;
        _converter = converter;
        _entityKeyFactory = entityKeyFactory;
        _logicalName = logicalName;
        _recordStore = recordStore;
        _validationReader = validationReader;
        _wrapperOptions = wrapperOptions;
        _entityLock = entityLock;
    }

    public string Name { get; }

    public Type SourceType => typeof(TSource);

    public Type TargetType => typeof(TTarget);

    public Task<MappingResult<TTarget>> ExecuteAsync(
        TSource source,
        MappingRunOptions runOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return ExecuteAsync(new[] { source }, runOptions, cancellationToken);
    }

    public async Task<MappingResult<TTarget>> ExecuteAsync(
        IEnumerable<TSource> sources,
        MappingRunOptions runOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        if (sources == null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        TSource[] sourceArray = sources.ToArray();
        List<MappedRecord<TTarget>> records = new(sourceArray.Length);
        List<MappingError> errors = new();

        MappingExecutionState state = new(
            _options.RequestSettings,
            runOptions?.ForceRefresh ?? false,
            _wrapperOptions.LockTimeout
        );

        foreach (TSource source in sourceArray)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TTarget target = new();
            if (target is Entity entityTarget)
            {
                ApplyLogicalName(entityTarget);
            }

            MappingContext<TSource, TTarget> context = new(source, target);
            foreach (IMappingStep<TSource, TTarget> step in _steps)
            {
                context.Stage = step.Stage;
                await step.ApplyAsync(context, state, cancellationToken).ConfigureAwait(false);
            }

            _options.PreSave?.Invoke(context);

            Entity mappedEntity = ConvertToEntity(target);
            if (mappedEntity != null && _entityKeyFactory != null)
            {
                ApplyEntityKey(mappedEntity, _entityKeyFactory(context));
            }

            if (mappedEntity == null)
            {
                records.Add(
                    new MappedRecord<TTarget>(
                        source,
                        target,
                        null,
                        null,
                        false,
                        false,
                        Array.Empty<string>()
                    )
                );
                errors.AddRange(context.Errors);
                continue;
            }

            MappedRecord<TTarget> record = await SaveAsync(
                    context,
                    mappedEntity,
                    state,
                    cancellationToken
                )
                .ConfigureAwait(false);
            records.Add(record);
            errors.AddRange(context.Errors);
        }

        bool validateAfterSave =
            runOptions?.ValidateAfterSave ?? _options.ValidateAfterSave;
        if (validateAfterSave)
        {
            errors.AddRange(
                await ValidateAsync(records, state.RequestSettings, cancellationToken)
                    .ConfigureAwait(false)
            );
        }

        return new MappingResult<TTarget>(records, errors, validateAfterSave);
    }

    async Task<IMappingResult> IMapper.ExecuteAsync(
        object source,
        MappingRunOptions runOptions,
        CancellationToken cancellationToken
    )
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (source is IEnumerable<TSource> enumerable && source is not string)
        {
            return await ExecuteAsync(enumerable, runOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        if (source is TSource typed)
        {
            return await ExecuteAsync(typed, runOptions, cancellationToken).ConfigureAwait(false);
        }

        throw new ArgumentException(
            $"Source must be {typeof(TSource).Name} or a collection of it.",
            nameof(source)
        );
    }

    private async Task<MappedRecord<TTarget>> SaveAsync(
        MappingContext<TSource, TTarget> context,
        Entity entity,
        MappingExecutionState state,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(entity.LogicalName))
        {
            throw new MapperConfigurationException("Mapped entity has no logical name.");
        }

        EntityKey key = EntityKey.FromEntity(entity);
        if (key == null)
        {
            return await CreateAsync(context, entity, null, state, cancellationToken)
                .ConfigureAwait(false);
        }

        using IEntityLockHandle lockHandle = await _entityLock
            .AcquireAsync(key, state.LockTimeout, cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<string> requiredColumns = entity.Attributes.Keys;
        bool useCacheForChangeDetection = _options.UseCacheForChangeDetection;
        Entity current = await _recordStore
            .GetAsync(
                key,
                requiredColumns,
                useCacheForChangeDetection,
                useCacheForChangeDetection ? state.ForceRefresh : true,
                state.RequestSettings,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (current == null)
        {
            return await CreateAsync(context, entity, key, state, cancellationToken)
                .ConfigureAwait(false);
        }

        entity.Id = current.Id;
        IReadOnlyList<string> changed = EntityChangeDetector.GetChangedFields(
            current,
            entity,
            _options.SkipFieldsToChanged
        );

        EntityReference reference = new(entity.LogicalName, current.Id);
        if (changed.Count == 0)
        {
            return new MappedRecord<TTarget>(
                context.Source,
                context.Target,
                reference,
                current,
                false,
                true,
                changed
            );
        }

        Entity update = new(entity.LogicalName, current.Id);
        foreach (string attribute in changed)
        {
            update[attribute] = entity[attribute];
        }

        await _sdkProxy.UpdateRecordAsync(update, state.RequestSettings).ConfigureAwait(false);
        await _recordStore.RemoveRecordAsync(entity, cancellationToken).ConfigureAwait(false);

        return new MappedRecord<TTarget>(
            context.Source,
            context.Target,
            reference,
            entity,
            false,
            false,
            changed
        );
    }

    private async Task<MappedRecord<TTarget>> CreateAsync(
        MappingContext<TSource, TTarget> context,
        Entity entity,
        EntityKey key,
        MappingExecutionState state,
        CancellationToken cancellationToken
    )
    {
        bool created = true;
        if (key != null && key.IsAlternateKey)
        {
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
                throw new MapperException(
                    $"Dataverse did not return a reference for created record '{key}'."
                );
            }

            entity.Id = response.Target.Id;
            created = response.RecordCreated;
        }
        else if (entity.Id != Guid.Empty)
        {
            CreateResponse response = await _sdkProxy
                .ExecuteAsync<CreateResponse>(
                    new CreateRequest { Target = entity },
                    state.RequestSettings
                )
                .ConfigureAwait(false);
            if (response != null && response.id != Guid.Empty)
            {
                entity.Id = response.id;
            }
        }
        else
        {
            Guid id = await _sdkProxy
                .CreateRecordAsync(entity, state.RequestSettings)
                .ConfigureAwait(false);
            entity.Id = id;
        }

        await _recordStore.RemoveRecordAsync(entity, cancellationToken).ConfigureAwait(false);
        await _recordStore.CacheSavedAsync(entity, cancellationToken).ConfigureAwait(false);

        return new MappedRecord<TTarget>(
            context.Source,
            context.Target,
            new EntityReference(entity.LogicalName, entity.Id),
            entity,
            created,
            false,
            entity.Attributes.Keys.ToArray()
        );
    }

    private Entity ConvertToEntity(TTarget target)
    {
        if (target is Entity entity)
        {
            ApplyLogicalName(entity);
            return entity;
        }

        if (_converter == null)
        {
            return null;
        }

        Entity converted = _converter.ToEntity(target);
        if (converted == null)
        {
            throw new MapperException("POCO target converter returned null.");
        }

        ApplyLogicalName(converted);
        return converted;
    }

    private void ApplyLogicalName(Entity entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.LogicalName))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_logicalName))
        {
            entity.LogicalName = _logicalName;
        }
    }

    private static void ApplyEntityKey(Entity entity, EntityKey key)
    {
        if (key == null)
        {
            return;
        }

        if (
            !string.IsNullOrWhiteSpace(entity.LogicalName)
            && !string.Equals(entity.LogicalName, key.LogicalName, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new MapperConfigurationException(
                $"Mapped entity logical name '{entity.LogicalName}' does not match key logical name '{key.LogicalName}'."
            );
        }

        entity.LogicalName = key.LogicalName;
        if (key.IsAlternateKey)
        {
            entity.Id = Guid.Empty;
            foreach (KeyValuePair<string, object> pair in key.AlternateKey)
            {
                entity.KeyAttributes[pair.Key] = pair.Value;
            }
        }
        else
        {
            entity.Id = key.Id.Value;
        }
    }

    private async Task<IReadOnlyList<MappingError>> ValidateAsync(
        IEnumerable<MappedRecord<TTarget>> records,
        RequestSettings requestSettings,
        CancellationToken cancellationToken
    )
    {
        if (_converter == null && !typeof(Entity).IsAssignableFrom(typeof(TTarget)))
        {
            throw new MapperConfigurationException(
                "ValidateAfterSave requires a converter for a POCO target."
            );
        }

        HashSet<string> fields = new(_options.ValidationFields, StringComparer.OrdinalIgnoreCase);
        if (fields.Count == 0)
        {
            foreach (MappedRecord<TTarget> record in records)
            {
                if (record.SavedEntity == null)
                {
                    continue;
                }

                foreach (string attribute in record.SavedEntity.Attributes.Keys)
                {
                    fields.Add(attribute);
                }
            }
        }

        List<ValidationTarget> targets = records
            .Where(p => p.Reference != null)
            .Select(
                p => new ValidationTarget(p.Reference, p.Source, p.Target, p.SavedEntity)
            )
            .ToList();

        return await _validationReader
            .ValidateAsync(
                targets,
                _ => fields,
                _options.Validator,
                Name,
                requestSettings,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

}
