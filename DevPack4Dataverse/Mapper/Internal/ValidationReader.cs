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

using DevPack4Dataverse.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.Mapper.Internal;

internal sealed class ValidationReader(SdkProxy sdkProxy)
{
    private const int MaxIdsPerQuery = 1000;

    public async Task<IReadOnlyList<MappingError>> ValidateAsync(
        IReadOnlyList<ValidationTarget> targets,
        ISet<string> fields,
        Func<Entity, MappingValidationContext, IEnumerable<string>> validator,
        string mapperName,
        RequestSettings requestSettings,
        CancellationToken cancellationToken
    )
    {
        List<MappingError> errors = new();
        List<ValidationTarget> validTargets = targets
            .Where(p => p?.Reference != null && p.Reference.Id != Guid.Empty)
            .ToList();

        Dictionary<Guid, Entity> records = await RetrieveAsync(
                validTargets,
                fields,
                requestSettings,
                cancellationToken
            )
            .ConfigureAwait(false);

        foreach (ValidationTarget target in validTargets)
        {
            if (!records.TryGetValue(target.Reference.Id, out Entity record))
            {
                errors.Add(
                    new MappingError(
                        target.Source,
                        target.Target,
                        null,
                        $"Saved record '{target.Reference.LogicalName}:{target.Reference.Id}' was not found."
                    )
                );
                continue;
            }

            if (validator == null)
            {
                continue;
            }

            MappingValidationContext context = new(
                mapperName,
                target.Source,
                target.Target,
                record
            );

            try
            {
                IEnumerable<string> messages = validator(record, context);
                if (messages == null)
                {
                    continue;
                }

                foreach (string message in messages)
                {
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        errors.Add(
                            new MappingError(target.Source, target.Target, null, message)
                        );
                    }
                }
            }
            catch (Exception exception)
            {
                errors.Add(
                    new MappingError(
                        target.Source,
                        target.Target,
                        null,
                        $"Validation failed: {exception.Message}",
                        exception
                    )
                );
            }
        }

        return errors;
    }

    private async Task<Dictionary<Guid, Entity>> RetrieveAsync(
        IReadOnlyList<ValidationTarget> targets,
        ISet<string> fields,
        RequestSettings requestSettings,
        CancellationToken cancellationToken
    )
    {
        Dictionary<Guid, Entity> result = new();
        string[] columns = fields?
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        foreach (
            IGrouping<string, ValidationTarget> group in targets.GroupBy(
                p => p.Reference.LogicalName,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            Guid[] ids = group.Select(p => p.Reference.Id).Distinct().ToArray();
            for (int offset = 0; offset < ids.Length; offset += MaxIdsPerQuery)
            {
                Guid[] chunk = ids.Skip(offset).Take(MaxIdsPerQuery).ToArray();
                QueryExpression query = new()
                {
                    EntityName = group.Key,
                    ColumnSet = columns.Length == 0 ? new ColumnSet(false) : new ColumnSet(columns)
                };
                query.Criteria.AddCondition(
                    "entityid",
                    ConditionOperator.In,
                    chunk.Cast<object>().ToArray()
                );

                Entity[] records = await sdkProxy
                    .RetrieveMultipleAsync(query, requestSettings)
                    .ConfigureAwait(false);

                foreach (Entity record in records)
                {
                    result[record.Id] = record;
                }
            }
        }

        return result;
    }
}

internal sealed class ValidationTarget(
    EntityReference reference,
    object source,
    object target,
    Entity savedEntity
)
{
    public EntityReference Reference { get; } = reference;

    public object Source { get; } = source;

    public object Target { get; } = target;

    public Entity SavedEntity { get; } = savedEntity;
}
