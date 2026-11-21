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

using Ardalis.GuardClauses;
using DevPack4Dataverse.Mapper;
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.BulkOperations;

/// <summary>
/// Validates the target collections used by the bulk operation messages.
/// </summary>
internal static class BulkOperationTargets
{
    public static EntityCollection PrepareEntities(
        EntityCollection? targets,
        bool requireIdentity,
        string parameterName
    )
    {
        Guard.Against.Null(targets, parameterName);

        if (targets.Entities == null || targets.Entities.Count == 0)
        {
            throw new ArgumentException("At least one target entity is required.", parameterName);
        }

        string entityName = targets.EntityName;
        if (string.IsNullOrWhiteSpace(entityName))
        {
            Entity firstEntity =
                targets.Entities[0]
                ?? throw new ArgumentException(
                    "Targets cannot contain a null entity.",
                    parameterName
                );
            entityName = firstEntity.LogicalName;
        }

        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException(
                "Targets must have an entity logical name.",
                parameterName
            );
        }

        HashSet<string> identities = new(StringComparer.Ordinal);
        foreach (Entity entity in targets.Entities)
        {
            if (entity == null)
            {
                throw new ArgumentException("Targets cannot contain a null entity.", parameterName);
            }

            if (!string.Equals(entity.LogicalName, entityName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"All targets must use the same table. Expected '{entityName}', found '{entity.LogicalName}'.",
                    parameterName
                );
            }

            if (requireIdentity)
            {
                if (
                    entity.Id == Guid.Empty
                    && (entity.KeyAttributes == null || entity.KeyAttributes.Count == 0)
                )
                {
                    throw new ArgumentException(
                        $"Target '{entityName}' must have an id or alternate-key values.",
                        parameterName
                    );
                }

                // A single UpdateMultiple applies only the first duplicate target and an
                // UpsertMultiple rejects duplicates. Without this guard, chunking the payload
                // turns those documented rules into a parallel last-writer-wins race.
                EntityKey identityKey = EntityKey.FromEntity(entity)!;
                if (!identities.Add(identityKey.ToString()))
                {
                    throw new ArgumentException(
                        $"Targets must not contain duplicate record identities. Duplicate: '{identityKey}'.",
                        parameterName
                    );
                }
            }
        }

        targets.EntityName = entityName;
        return targets;
    }

    public static EntityReferenceCollection PrepareReferences(
        EntityReferenceCollection? targets,
        string parameterName
    )
    {
        Guard.Against.Null(targets, parameterName);

        if (targets.Count == 0)
        {
            throw new ArgumentException("At least one target reference is required.", parameterName);
        }

        string? entityName = null;
        foreach (EntityReference reference in targets)
        {
            if (reference == null)
            {
                throw new ArgumentException(
                    "Targets cannot contain a null entity reference.",
                    parameterName
                );
            }

            Guard.Against.NullOrWhiteSpace(
                reference.LogicalName,
                $"Target reference must have a logical name.",
                parameterName
            );

            entityName ??= reference.LogicalName;
            if (!string.Equals(reference.LogicalName, entityName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"All targets must use the same table. Expected '{entityName}', found '{reference.LogicalName}'.",
                    parameterName
                );
            }

            if (
                reference.Id == Guid.Empty
                && (reference.KeyAttributes == null || reference.KeyAttributes.Count == 0)
            )
            {
                throw new ArgumentException(
                    $"Target reference '{reference.LogicalName}' must have an id or alternate-key values.",
                    parameterName
                );
            }
        }

        return targets;
    }
}
