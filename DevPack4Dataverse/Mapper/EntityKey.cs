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

using System.Globalization;
using System.Text;
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.Mapper;

/// <summary>
/// Identifies a Dataverse record by a basic key (logical name + id) or an alternate key.
/// </summary>
public sealed class EntityKey : IEquatable<EntityKey>
{
    private readonly Dictionary<string, object> _alternateKey;
    private readonly string _normalized;

    private EntityKey(
        string logicalName,
        Guid? id,
        IEnumerable<KeyValuePair<string, object>> alternateKey
    )
    {
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            throw new ArgumentException("Logical name is required.", nameof(logicalName));
        }

        LogicalName = logicalName;
        Id = id;
        _alternateKey = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (alternateKey != null)
        {
            foreach (KeyValuePair<string, object> pair in alternateKey)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    throw new ArgumentException("Alternate key attribute names cannot be empty.", nameof(alternateKey));
                }

                _alternateKey[pair.Key] = pair.Value;
            }
        }

        _normalized = BuildNormalized();
    }

    /// <summary>Dataverse logical entity name.</summary>
    public string LogicalName { get; }

    /// <summary>Basic key id, if this is a basic-key reference.</summary>
    public Guid? Id { get; }

    /// <summary>Alternate key values when this is an alternate-key reference.</summary>
    public IReadOnlyDictionary<string, object> AlternateKey => _alternateKey;

    /// <summary>True when the key has no usable id or alternate-key values.</summary>
    public bool IsEmpty =>
        (!Id.HasValue || Id.Value == Guid.Empty)
        && !_alternateKey.Values.Any(p => p != null && !(p is string s && s.Length == 0));

    /// <summary>True when the key uses alternate-key values.</summary>
    public bool IsAlternateKey => _alternateKey.Count > 0;

    public static EntityKey ById(string logicalName, Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Entity id cannot be empty.", nameof(id));
        }

        return new EntityKey(logicalName, id, null);
    }

    public static EntityKey ByAlternateKey(
        string logicalName,
        params KeyValuePair<string, object>[] attributes
    )
    {
        if (attributes == null || attributes.Length == 0)
        {
            throw new ArgumentException("At least one alternate key attribute is required.", nameof(attributes));
        }

        return new EntityKey(logicalName, null, attributes);
    }

    public static EntityKey ByAlternateKey(
        string logicalName,
        IDictionary<string, object> attributes
    )
    {
        if (attributes == null || attributes.Count == 0)
        {
            throw new ArgumentException("At least one alternate key attribute is required.", nameof(attributes));
        }

        return new EntityKey(logicalName, null, attributes);
    }

    public static EntityKey ByAlternateKey(
        string logicalName,
        IEnumerable<KeyValuePair<string, object>> attributes
    )
    {
        if (attributes == null)
        {
            throw new ArgumentException("At least one alternate key attribute is required.", nameof(attributes));
        }

        KeyValuePair<string, object>[] values = attributes.ToArray();
        if (values.Length == 0)
        {
            throw new ArgumentException("At least one alternate key attribute is required.", nameof(attributes));
        }

        return new EntityKey(logicalName, null, values);
    }

    public static EntityKey FromEntityReference(EntityReference reference)
    {
        if (reference == null)
        {
            return null;
        }

        if (reference.KeyAttributes != null && reference.KeyAttributes.Count > 0)
        {
            return ByAlternateKey(reference.LogicalName, reference.KeyAttributes);
        }

        return reference.Id == Guid.Empty ? null : ById(reference.LogicalName, reference.Id);
    }

    public static EntityKey FromEntity(Entity entity)
    {
        if (entity == null)
        {
            return null;
        }

        if (entity.Id != Guid.Empty)
        {
            return ById(entity.LogicalName, entity.Id);
        }

        if (entity.KeyAttributes != null && entity.KeyAttributes.Count > 0)
        {
            return ByAlternateKey(entity.LogicalName, entity.KeyAttributes);
        }

        return null;
    }

    /// <summary>
    /// Creates an <see cref="EntityReference"/> for retrieval or lookup assignment.
    /// </summary>
    public EntityReference ToEntityReference()
    {
        if (IsAlternateKey)
        {
            EntityReference reference = new(LogicalName);
            foreach (KeyValuePair<string, object> pair in _alternateKey)
            {
                reference.KeyAttributes[pair.Key] = pair.Value;
            }

            return reference;
        }

        return new EntityReference(LogicalName, Id.Value);
    }

    public bool Equals(EntityKey other) =>
        other != null && string.Equals(_normalized, other._normalized, StringComparison.Ordinal);

    public override bool Equals(object obj) => Equals(obj as EntityKey);

    public override int GetHashCode() => _normalized.GetHashCode();

    public override string ToString() => _normalized;

    private string BuildNormalized()
    {
        StringBuilder builder = new();
        builder.Append(LogicalName.ToLowerInvariant());

        if (Id.HasValue && Id.Value != Guid.Empty)
        {
            builder.Append("|id=").Append(Id.Value.ToString("N"));
            return builder.ToString();
        }

        builder.Append("|ak=");
        foreach (
            KeyValuePair<string, object> pair in _alternateKey.OrderBy(
                p => p.Key,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            builder
                .Append(pair.Key.ToLowerInvariant())
                .Append('=')
                .Append(NormalizeValue(pair.Value))
                .Append(';');
        }

        return builder.ToString();
    }

    private static string NormalizeValue(object value)
    {
        switch (value)
        {
            case null:
                return "null";
            case Guid guid:
                return "g:" + guid.ToString("N");
            case DateTime dateTime:
                return "d:" + dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            case byte[] bytes:
                return "b64:" + Convert.ToBase64String(bytes);
            case EntityReference reference:
                return "er:" + reference.LogicalName.ToLowerInvariant() + ":" + reference.Id.ToString("N");
            case OptionSetValue optionSetValue:
                return "os:" + optionSetValue.Value.ToString(CultureInfo.InvariantCulture);
            case Money money:
                return "m:" + money.Value.ToString(CultureInfo.InvariantCulture);
            case IFormattable formattable:
                return "v:" + formattable.ToString(null, CultureInfo.InvariantCulture);
            default:
                return "s:" + value;
        }
    }
}
