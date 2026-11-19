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

internal static class EntityChangeDetector
{
    public static IReadOnlyList<string> GetChangedFields(
        Entity current,
        Entity mapped,
        ISet<string> skipFields
    )
    {
        if (mapped == null)
        {
            return Array.Empty<string>();
        }

        if (current == null)
        {
            return mapped.Attributes.Keys.ToArray();
        }

        List<string> changed = new();
        foreach (KeyValuePair<string, object> attribute in mapped.Attributes)
        {
            if (skipFields != null && skipFields.Contains(attribute.Key))
            {
                continue;
            }

            if (
                !current.Attributes.TryGetValue(attribute.Key, out object currentValue)
                && attribute.Value == null
            )
            {
                continue;
            }

            if (!current.Attributes.ContainsKey(attribute.Key))
            {
                changed.Add(attribute.Key);
                continue;
            }

            if (!ValueComparer.AreEqual(currentValue, attribute.Value))
            {
                changed.Add(attribute.Key);
            }
        }

        return changed;
    }
}
