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
    private static readonly IReadOnlyDictionary<string, object> EmptyAlternateKey =
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, object> _alternateKey;
    private readonly string _normalized;

    private EntityKey(
        string logicalName,
        Guid? id,
        IEnumerable<KeyValuePair<string, object>>? alternateKey
    )
    {
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            throw new ArgumentException("Logical name is required.", nameof(logicalName));
        }

        LogicalName = logicalName.ToLowerInvariant();
        Id = id;
        if (alternateKey == null)
        {
            _alternateKey = EmptyAlternateKey;
        }
        else
        {
            Dictionary<string, object> values = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object> pair in alternateKey)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    throw new ArgumentException(
                        "Alternate key attribute names cannot be empty.",
                        nameof(alternateKey)
                    );
                }

                values[pair.Key.ToLowerInvariant()] = pair.Value;
            }

            _alternateKey = values;
        }

        _normalized = BuildNormalized();
    }

    private EntityKey(
        string logicalName,
        Guid? id,
        IReadOnlyDictionary<string, object>? alternateKey
    )
    {
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            throw new ArgumentException("Logical name is required.", nameof(logicalName));
        }

        LogicalName = logicalName.ToLowerInvariant();
        Id = id;
        _alternateKey = alternateKey ?? EmptyAlternateKey;
        _normalized = BuildNormalized();
    }

    internal static EntityKey FromAlternateKeyValues(
        string logicalName,
        Dictionary<string, object> values
    ) => new(logicalName, null, values);

    private EntityKey(string logicalName, string attributeName, object attributeValue)
    {
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            throw new ArgumentException("Logical name is required.", nameof(logicalName));
        }
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            throw new ArgumentException(
                "Alternate key attribute name is required.",
                nameof(attributeName)
            );
        }

        LogicalName = logicalName.ToLowerInvariant();
        _alternateKey = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [attributeName.ToLowerInvariant()] = attributeValue
        };
        _normalized = BuildNormalized();
    }

    /// <summary>Dataverse logical entity name.</summary>
    public string LogicalName { get; }

    /// <summary>Basic key id, if this is a basic-key reference.</summary>
    public Guid? Id { get; }

    /// <summary>Alternate key values when this is an alternate-key reference.</summary>
    public IReadOnlyDictionary<string, object> AlternateKey => _alternateKey;

    /// <summary>True when the key has no usable id or alternate-key values.</summary>
    public bool IsEmpty
    {
        get
        {
            if (Id.HasValue && Id.Value != Guid.Empty)
            {
                return false;
            }

            if (_alternateKey.Count == 0)
            {
                return true;
            }

            // Every part of a composite alternate key is required. One null or blank part
            // makes the whole key unusable; querying with it would match the wrong rows.
            foreach (object value in _alternateKey.Values)
            {
                if (value == null || (value is string text && string.IsNullOrWhiteSpace(text)))
                {
                    return true;
                }
            }

            return false;
        }
    }

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
        KeyValuePair<string, object> first,
        KeyValuePair<string, object> second
    )
    {
        if (string.IsNullOrWhiteSpace(first.Key) || string.IsNullOrWhiteSpace(second.Key))
        {
            throw new ArgumentException(
                "Alternate key attribute names cannot be empty.",
                nameof(first)
            );
        }

        Dictionary<string, object> values = new(2, StringComparer.OrdinalIgnoreCase)
        {
            [first.Key.ToLowerInvariant()] = first.Value,
            [second.Key.ToLowerInvariant()] = second.Value
        };
        return FromAlternateKeyValues(logicalName, values);
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
        string attributeName,
        object attributeValue
    ) => new(logicalName, attributeName, attributeValue);

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

    public static EntityKey? FromEntityReference(EntityReference? reference)
    {
        if (reference == null)
        {
            return null;
        }

        if (reference.Id != Guid.Empty)
        {
            return ById(reference.LogicalName, reference.Id);
        }

        if (reference.KeyAttributes != null && reference.KeyAttributes.Count > 0)
        {
            return ByAlternateKey(reference.LogicalName, reference.KeyAttributes);
        }

        return null;
    }

    public static EntityKey? FromEntity(Entity? entity)
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

        return new EntityReference(LogicalName, Id!.Value);
    }

    public bool Equals(EntityKey? other) =>
        other != null && string.Equals(_normalized, other._normalized, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as EntityKey);

    public override int GetHashCode() => _normalized.GetHashCode();

    public override string ToString() => _normalized;

    private string BuildNormalized()
    {
        StringBuilder builder = new(64);
        builder.Append(LogicalName);

        if (Id.HasValue && Id.Value != Guid.Empty)
        {
            builder.Append("|id=").Append(Id.Value.ToString("N"));
            return builder.ToString();
        }

        builder.Append("|ak=");
        if (_alternateKey.Count == 1)
        {
            foreach (KeyValuePair<string, object> pair in _alternateKey)
            {
                AppendAlternateKeyPart(builder, pair);
            }
        }
        else
        {
            KeyValuePair<string, object>[] pairs = _alternateKey.ToArray();
            Array.Sort(
                pairs,
                static (left, right) =>
                    StringComparer.OrdinalIgnoreCase.Compare(left.Key, right.Key)
            );
            foreach (KeyValuePair<string, object> pair in pairs)
            {
                AppendAlternateKeyPart(builder, pair);
            }
        }

        return builder.ToString();
    }

    private static void AppendAlternateKeyPart(
        StringBuilder builder,
        KeyValuePair<string, object> pair
    )
    {
        string key = pair.Key;
        string value = NormalizeValue(pair.Value);
        builder
            .Append(key.Length)
            .Append(':')
            .Append(key)
            .Append(value.Length)
            .Append(':')
            .Append(value);
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
                if (reference.Id != Guid.Empty)
                {
                    return "er:"
                        + reference.LogicalName.ToLowerInvariant()
                        + ":"
                        + reference.Id.ToString("N");
                }

                if (reference.KeyAttributes == null || reference.KeyAttributes.Count == 0)
                {
                    return "er:" + reference.LogicalName.ToLowerInvariant() + ":empty";
                }

                KeyValuePair<string, object>[] referenceKeys = reference.KeyAttributes.ToArray();
                Array.Sort(
                    referenceKeys,
                    static (left, right) =>
                        StringComparer.OrdinalIgnoreCase.Compare(left.Key, right.Key)
                );
                StringBuilder referenceBuilder = new();
                foreach (KeyValuePair<string, object> pair in referenceKeys)
                {
                    AppendAlternateKeyPart(referenceBuilder, pair);
                }

                return "er:"
                    + reference.LogicalName.ToLowerInvariant()
                    + "{"
                    + referenceBuilder
                    + "}";
            case OptionSetValue optionSetValue:
                return "os:" + optionSetValue.Value.ToString(CultureInfo.InvariantCulture);
            case Money money:
                return "m:" + money.Value.ToString(CultureInfo.InvariantCulture);
            case decimal decimalValue:
                return "d:" + decimalValue.ToString("G29", CultureInfo.InvariantCulture);
            case double doubleValue:
                return "db:" + doubleValue.ToString("R", CultureInfo.InvariantCulture);
            case float floatValue:
                return "f:" + floatValue.ToString("R", CultureInfo.InvariantCulture);
            case string text:
                return "s:" + text.ToLowerInvariant();
            case IFormattable formattable:
                return "v:" + formattable.ToString(null, CultureInfo.InvariantCulture);
            default:
                return "s:" + value;
        }
    }
}
