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
using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Mapper.Internal;
using DevPack4Dataverse.Mapper.Lock;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.Mapper;

/// <summary>
/// Global mapper container. Owns shared cache and lock instances and applies wrapper-level validation.
/// </summary>
public sealed class MapperWrapper
{
    private readonly MapperWrapperOptions _effectiveOptions;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, IMapper> _mappers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _preloadedAt =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SdkProxy _sdkProxy;
    private readonly ValidationReader _validationReader;

    public MapperWrapper(
        SdkProxy sdkProxy,
        ILogger logger,
        MapperWrapperOptions? options = null,
        IEntityCache? cache = null,
        IEntityLock? entityLock = null
    )
    {
        _sdkProxy = sdkProxy ?? throw new ArgumentNullException(nameof(sdkProxy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Options = options ?? new MapperWrapperOptions();
        _effectiveOptions = Options.Snapshot();
        Cache = cache ?? new InMemoryEntityCache();
        Lock = entityLock ?? new InMemoryEntityLock();
        _validationReader = new ValidationReader(
            _sdkProxy,
            new PrimaryKeyAttributeProvider(_sdkProxy)
        );
    }

    /// <summary>
    /// Options captured when the wrapper was created. Change them before constructing the
    /// wrapper; later mutations do not affect existing mappers.
    /// </summary>
    public MapperWrapperOptions Options { get; }

    public IEntityCache Cache { get; }

    public IEntityLock Lock { get; }

    public MapperBuilder<TSource, TTarget> CreateMapperBuilder<TSource, TTarget>(
        string? name = null
    ) where TTarget : class, new()
    {
        return new MapperBuilder<TSource, TTarget>(
            _sdkProxy,
            _logger,
            _effectiveOptions,
            Cache,
            Lock,
            name,
            _preloadedAt
        );
    }

    public bool Register(IMapper mapper)
    {
        if (mapper == null)
        {
            throw new ArgumentNullException(nameof(mapper));
        }

        return _mappers.TryAdd(mapper.Name, mapper);
    }

    public bool Register(string name, IMapper mapper)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Mapper name is required.", nameof(name));
        }

        if (mapper == null)
        {
            throw new ArgumentNullException(nameof(mapper));
        }

        return _mappers.TryAdd(name, mapper);
    }

    public bool TryGet(string name, out IMapper? mapper)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Mapper name is required.", nameof(name));
        }

        return _mappers.TryGetValue(name, out mapper);
    }

    public IMapper Get(string name)
    {
        if (!TryGet(name, out IMapper? mapper))
        {
            throw new MapperException($"Mapper '{name}' is not registered.");
        }

        return mapper!;
    }

    public async Task<MapperWrapperResult> ExecuteAsync(
        string name,
        object source,
        MappingRunOptions? runOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        IMapper mapper = Get(name);
        IMappingResult result = await mapper
            .ExecuteAsync(source, runOptions, cancellationToken)
            .ConfigureAwait(false);

        List<MappingError> errors = new(result.Errors);
        if (
            runOptions?.ValidateAfterSave != false
            && _effectiveOptions.ValidateAfterSave
            && (_effectiveOptions.ValidateAfterMapperValidation || !ValidationWasRun(result))
        )
        {
            try
            {
                errors.AddRange(
                    await ValidateAsync(result.SavedReferences, cancellationToken)
                        .ConfigureAwait(false)
                );
            }
            catch (Exception exception)
            {
                exception.Data[MapperWrapperResult.PartialResultsDataKey] =
                    new MapperWrapperResult(result, errors.ToArray());
                throw;
            }
        }

        return new MapperWrapperResult(result, errors);
    }

    /// <summary>
    /// Executes several registered mappers as one batch and validates the combined saved records once.
    /// </summary>
    public async Task<MapperBatchResult> ExecuteAllAsync(
        IEnumerable<MapperExecution> executions,
        CancellationToken cancellationToken = default
    )
    {
        if (executions == null)
        {
            throw new ArgumentNullException(nameof(executions));
        }

        List<IMappingResult> results = new();
        List<MappingError> errors = new();
        List<EntityReference> referencesToValidate = new();
        List<MapperExecution> executionList = executions.ToList();
        if (executionList.Any(p => p == null))
        {
            throw new ArgumentException("Mapper executions cannot contain null.", nameof(executions));
        }

        IMapper[] mappers = executionList.Select(p => Get(p.Name)).ToArray();
        for (int index = 0; index < executionList.Count; index++)
        {
            MapperExecution execution = executionList[index];
            try
            {
                IMappingResult result = await mappers[index]
                    .ExecuteAsync(execution.Source, execution.RunOptions, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(result);
                errors.AddRange(result.Errors);

                if (
                    execution.RunOptions?.ValidateAfterSave != false
                    && (
                        _effectiveOptions.ValidateAfterMapperValidation
                        || !ValidationWasRun(result)
                    )
                )
                {
                    referencesToValidate.AddRange(result.SavedReferences);
                }
            }
            catch (Exception exception)
            {
                exception.Data[MapperBatchResult.PartialResultsDataKey] = new MapperBatchResult(
                    results.ToArray(),
                    errors.ToArray()
                );
                throw;
            }
        }

        if (_effectiveOptions.ValidateAfterSave)
        {
            try
            {
                errors.AddRange(
                    await ValidateAsync(referencesToValidate, cancellationToken)
                        .ConfigureAwait(false)
                );
            }
            catch (Exception exception)
            {
                exception.Data[MapperBatchResult.PartialResultsDataKey] = new MapperBatchResult(
                    results.ToArray(),
                    errors.ToArray()
                );
                throw;
            }
        }

        return new MapperBatchResult(results, errors);
    }

    public async Task<IReadOnlyList<MappingError>> ValidateAsync(
        IEnumerable<EntityReference> savedRecords,
        CancellationToken cancellationToken = default
    )
    {
        if (savedRecords == null)
        {
            throw new ArgumentNullException(nameof(savedRecords));
        }

        if (
            _effectiveOptions.Validator != null
            && _effectiveOptions.ValidationFields.Count == 0
            && _effectiveOptions.ValidationFieldsByEntity.Count == 0
        )
        {
            throw new MapperConfigurationException(
                "Wrapper ValidateAfterSave requires ValidationFields or ValidationFieldsByEntity."
            );
        }

        List<ValidationTarget> targets = savedRecords
            .Where(p => p != null)
            .Select(p => new ValidationTarget(p, null, null, null))
            .ToList();

        if (_effectiveOptions.Validator != null)
        {
            foreach (
                IGrouping<string, ValidationTarget> group in targets.GroupBy(
                    p => p.Reference.LogicalName,
                    StringComparer.OrdinalIgnoreCase
                )
            )
            {
                if (_effectiveOptions.GetValidationFields(group.Key).Count == 0)
                {
                    throw new MapperConfigurationException(
                        $"Wrapper ValidateAfterSave has no validation fields for '{group.Key}'."
                    );
                }
            }
        }

        return await _validationReader
            .ValidateAsync(
                targets,
                _effectiveOptions.GetValidationFields,
                _effectiveOptions.Validator,
                null,
                _effectiveOptions.RequestSettings,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static bool ValidationWasRun(IMappingResult result) =>
        result is IValidationAwareMappingResult aware && aware.ValidationWasRun;
}
