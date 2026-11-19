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

using System.Linq.Expressions;
using System.Reflection;
using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Mapper.Internal;
using DevPack4Dataverse.Mapper.Lock;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.Mapper;

/// <summary>
/// Configures one mapper. Builders are mutable and not thread-safe; build one mapper per definition.
/// </summary>
public sealed class MapperBuilder<TSource, TTarget>
    where TTarget : class, new()
{
    private readonly IEntityCache _cache;
    private readonly IEntityLock _entityLock;
    private readonly ILogger _logger;
    private readonly LookupResolver _lookupResolver;
    private readonly HashSet<string> _members = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _name;
    private readonly MapperOptions<TSource, TTarget> _options = new();
    private readonly RecordStore _recordStore;
    private readonly SdkProxy _sdkProxy;
    private readonly List<IMappingStep<TSource, TTarget>> _steps = new();
    private readonly ValidationReader _validationReader;
    private readonly MapperWrapperOptions _wrapperOptions;
    private IModelEntityConverter<TTarget> _converter;
    private Func<MappingContext<TSource, TTarget>, EntityKey> _entityKeyFactory;
    private int _currentStage = 1;
    private string _logicalName;

    internal MapperBuilder(
        SdkProxy sdkProxy,
        ILogger logger,
        MapperWrapperOptions wrapperOptions,
        IEntityCache cache,
        IEntityLock entityLock,
        string name
    )
    {
        _sdkProxy = sdkProxy ?? throw new ArgumentNullException(nameof(sdkProxy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _wrapperOptions = wrapperOptions ?? throw new ArgumentNullException(nameof(wrapperOptions));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _entityLock = entityLock ?? throw new ArgumentNullException(nameof(entityLock));
        _name = name;
        _recordStore = new RecordStore(_sdkProxy, _logger, _cache, _wrapperOptions);
        _lookupResolver = new LookupResolver(
            _sdkProxy,
            _entityLock,
            _recordStore
        );
        _validationReader = new ValidationReader(_sdkProxy);
    }

    /// <summary>
    /// Maps a late-bound entity attribute.
    /// </summary>
    public MapperBuilder<TSource, TTarget> Map<TValue>(
        string targetAttribute,
        Func<MappingContext<TSource, TTarget>, TValue> value
    )
    {
        EnsureEntityTarget();
        EnsureTargetAttribute(targetAttribute);
        EnsureNotMapped(targetAttribute);
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        _steps.Add(
            new MappingStep<TSource, TTarget>(
                _currentStage,
                targetAttribute,
                context => ((Entity)(object)context.Target)[targetAttribute] = value(context)
            )
        );

        return this;
    }

    /// <summary>
    /// Maps a POCO or early-bound property.
    /// </summary>
    public MapperBuilder<TSource, TTarget> Map<TValue>(
        Expression<Func<TTarget, TValue>> target,
        Func<MappingContext<TSource, TTarget>, TValue> value
    )
    {
        MemberDescriptor member = ParseMember(target);
        EnsureNotMapped(member.Name);
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        _steps.Add(
            new MappingStep<TSource, TTarget>(
                _currentStage,
                member.Name,
                context => member.Setter(context.Target, value(context))
            )
        );

        return this;
    }

    /// <summary>
    /// Maps a lookup on a late-bound entity.
    /// </summary>
    public MapperBuilder<TSource, TTarget> MapLookup(
        string targetAttribute,
        Func<MappingContext<TSource, TTarget>, EntityKey> key,
        LookupMappingOptions options,
        Func<MappingContext<TSource, TTarget>, Entity> createFactory = null
    )
    {
        EnsureEntityTarget();
        EnsureTargetAttribute(targetAttribute);
        EnsureNotMapped(targetAttribute);
        ValidateLookupOptions(targetAttribute, options, createFactory);
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        _steps.Add(
            new LookupMappingStep<TSource, TTarget>(
                _currentStage,
                targetAttribute,
                (target, reference) => ((Entity)(object)target)[targetAttribute] = reference,
                key,
                createFactory,
                options,
                _lookupResolver
            )
        );

        return this;
    }

    /// <summary>
    /// Maps a lookup on a POCO or early-bound property.
    /// </summary>
    public MapperBuilder<TSource, TTarget> MapLookup(
        Expression<Func<TTarget, EntityReference>> target,
        Func<MappingContext<TSource, TTarget>, EntityKey> key,
        LookupMappingOptions options,
        Func<MappingContext<TSource, TTarget>, Entity> createFactory = null
    )
    {
        MemberDescriptor member = ParseMember(target);
        EnsureNotMapped(member.Name);
        ValidateLookupOptions(member.Name, options, createFactory);
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        _steps.Add(
            new LookupMappingStep<TSource, TTarget>(
                _currentStage,
                member.Name,
                (target, reference) => member.Setter(target, reference),
                key,
                createFactory,
                options,
                _lookupResolver
            )
        );

        return this;
    }

    /// <summary>
    /// Starts the next mapping stage. Later stages can read values mapped by earlier stages.
    /// </summary>
    public MapperBuilder<TSource, TTarget> NextStage()
    {
        _currentStage++;
        return this;
    }

    /// <summary>
    /// Sets the Dataverse logical name for a late-bound <see cref="Entity"/> target.
    /// </summary>
    public MapperBuilder<TSource, TTarget> WithLogicalName(string logicalName)
    {
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            throw new ArgumentException("Logical name is required.", nameof(logicalName));
        }

        _logicalName = logicalName;
        return this;
    }

    /// <summary>
    /// Sets the converter used to save and validate a POCO target.
    /// </summary>
    public MapperBuilder<TSource, TTarget> WithConverter(
        IModelEntityConverter<TTarget> converter
    )
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        return this;
    }

    /// <summary>
    /// Sets the save key for an existing record (basic id or alternate key).
    /// </summary>
    public MapperBuilder<TSource, TTarget> WithEntityKey(
        Func<MappingContext<TSource, TTarget>, EntityKey> keyFactory
    )
    {
        _entityKeyFactory = keyFactory ?? throw new ArgumentNullException(nameof(keyFactory));
        return this;
    }

    /// <summary>
    /// Configures mapper options.
    /// </summary>
    public MapperBuilder<TSource, TTarget> WithOptions(
        Action<MapperOptions<TSource, TTarget>> configure
    )
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        configure(_options);
        return this;
    }

    /// <summary>
    /// Creates an immutable mapper.
    /// </summary>
    public Mapper<TSource, TTarget> Build()
    {
        if (_steps.Count == 0)
        {
            throw new MapperConfigurationException("A mapper must contain at least one mapping.");
        }

        if (
            typeof(TTarget) == typeof(Entity)
            && string.IsNullOrWhiteSpace(_logicalName)
            && _entityKeyFactory == null
        )
        {
            throw new MapperConfigurationException(
                "A late-bound Entity target requires WithLogicalName() or WithEntityKey()."
            );
        }

        if (
            !typeof(Entity).IsAssignableFrom(typeof(TTarget))
            && _converter == null
            && _options.ValidateAfterSave
        )
        {
            throw new MapperConfigurationException(
                "ValidateAfterSave requires a converter for a POCO target."
            );
        }

        string name = string.IsNullOrWhiteSpace(_name)
            ? $"{typeof(TSource).Name}->{typeof(TTarget).Name}"
            : _name;

        return new Mapper<TSource, TTarget>(
            name,
            _sdkProxy,
            _steps.ToArray(),
            _options,
            _converter,
            _entityKeyFactory,
            _logicalName,
            _recordStore,
            _validationReader,
            _wrapperOptions,
            _cache,
            _entityLock
        );
    }

    private static MemberDescriptor ParseMember(LambdaExpression expression)
    {
        Expression body = expression?.Body;
        while (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            body = unary.Operand;
        }

        if (body is not MemberExpression memberExpression)
        {
            throw new MapperConfigurationException(
                "Target mapping must be a property or field access expression."
            );
        }

        if (memberExpression.Member is PropertyInfo property)
        {
            return new MemberDescriptor(
                property.Name,
                (target, value) => property.SetValue(target, value, null)
            );
        }

        if (memberExpression.Member is FieldInfo field)
        {
            return new MemberDescriptor(field.Name, (target, value) => field.SetValue(target, value));
        }

        throw new MapperConfigurationException(
            "Target mapping must be a property or field access expression."
        );
    }

    private void EnsureEntityTarget()
    {
        if (!typeof(Entity).IsAssignableFrom(typeof(TTarget)))
        {
            throw new MapperConfigurationException(
                "Attribute-name mapping requires an Entity target. Use an expression mapping for POCO targets."
            );
        }
    }

    private static void EnsureTargetAttribute(string targetAttribute)
    {
        if (string.IsNullOrWhiteSpace(targetAttribute))
        {
            throw new ArgumentException("Target attribute is required.", nameof(targetAttribute));
        }
    }

    private void EnsureNotMapped(string targetMember)
    {
        if (!_members.Add(targetMember))
        {
            throw new MapperConfigurationException(
                $"Target member '{targetMember}' is mapped more than once."
            );
        }
    }

    private static void ValidateLookupOptions(
        string targetMember,
        LookupMappingOptions options,
        Func<MappingContext<TSource, TTarget>, Entity> createFactory
    )
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.Validate(targetMember);
        if (options.CreateIfNotExists && createFactory == null)
        {
            throw new MapperConfigurationException(
                $"Lookup '{targetMember}' has CreateIfNotExists enabled but no create factory."
            );
        }
    }

    private sealed class MemberDescriptor(string name, Action<TTarget, object> setter)
    {
        public string Name { get; } = name;
        public Action<TTarget, object> Setter { get; } = setter;
    }
}
