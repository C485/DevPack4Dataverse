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

using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.Mapper.Internal;

internal static class EntityCopy
{
    public static Entity? Clone(Entity entity)
    {
        if (entity == null)
        {
            return null;
        }

        Entity clone = new(entity.LogicalName, entity.Id)
        {
            RowVersion = entity.RowVersion
        };
        foreach (KeyValuePair<string, object> attribute in entity.Attributes)
        {
            clone[attribute.Key] = CloneValue(attribute.Value);
        }

        if (entity.KeyAttributes != null)
        {
            foreach (KeyValuePair<string, object> key in entity.KeyAttributes)
            {
                clone.KeyAttributes[key.Key] = CloneValue(key.Value);
            }
        }

        foreach (KeyValuePair<string, string> formatted in entity.FormattedValues)
        {
            clone.FormattedValues[formatted.Key] = formatted.Value;
        }

        return clone;
    }

    public static void Merge(Entity target, Entity source)
    {
        if (target == null || source == null)
        {
            return;
        }

        foreach (KeyValuePair<string, object> attribute in source.Attributes)
        {
            target[attribute.Key] = CloneValue(attribute.Value);
        }

        foreach (KeyValuePair<string, string> formatted in source.FormattedValues)
        {
            target.FormattedValues[formatted.Key] = formatted.Value;
        }

        if (!string.IsNullOrEmpty(source.RowVersion))
        {
            target.RowVersion = source.RowVersion;
        }
    }

    private static object? CloneValue(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case byte[] bytes:
                return bytes.ToArray();
            case EntityReference reference:
                return CloneReference(reference);
            case OptionSetValueCollection options:
                OptionSetValueCollection clonedOptions = new();
                foreach (OptionSetValue option in options)
                {
                    clonedOptions.Add(new OptionSetValue(option.Value));
                }

                return clonedOptions;
            default:
                return value;
        }
    }

    private static EntityReference CloneReference(EntityReference reference)
    {
        EntityReference clone = new(reference.LogicalName, reference.Id)
        {
            Name = reference.Name
        };
        if (reference.KeyAttributes != null)
        {
            foreach (KeyValuePair<string, object> key in reference.KeyAttributes)
            {
                clone.KeyAttributes[key.Key] = CloneValue(key.Value);
            }
        }

        return clone;
    }
}
