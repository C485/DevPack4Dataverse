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
        Func<string, ISet<string>>? fieldsProvider,
        Func<Entity, MappingValidationContext, IEnumerable<string>>? validator,
        string? mapperName,
        RequestSettings? requestSettings,
        CancellationToken cancellationToken
    )
    {
        List<MappingError> errors = new();
        List<ValidationTarget> validTargets = new();
        foreach (ValidationTarget target in targets)
        {
            if (target?.Reference == null)
            {
                continue;
            }

            if (target.Reference.Id == Guid.Empty)
            {
                errors.Add(
                    new MappingError(
                        target.Source,
                        target.Target,
                        null,
                        $"Saved record reference '{target.Reference.LogicalName}' has no id."
                    )
                );
                continue;
            }

            validTargets.Add(target);
        }

        Dictionary<string, Entity> records = await RetrieveAsync(
                validTargets,
                fieldsProvider,
                requestSettings,
                cancellationToken
            )
            .ConfigureAwait(false);

        foreach (ValidationTarget target in validTargets)
        {
            string key = ValidationKey(target.Reference.LogicalName, target.Reference.Id);
            if (!records.TryGetValue(key, out Entity? record))
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

    private async Task<Dictionary<string, Entity>> RetrieveAsync(
        IReadOnlyList<ValidationTarget> targets,
        Func<string, ISet<string>>? fieldsProvider,
        RequestSettings? requestSettings,
        CancellationToken cancellationToken
    )
    {
        Dictionary<string, Entity> result = new();
        string[] columns = Array.Empty<string>();

        foreach (
            IGrouping<string, ValidationTarget> group in targets.GroupBy(
                p => p.Reference.LogicalName,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            ISet<string>? fields = fieldsProvider?.Invoke(group.Key);
            columns =
                fields?
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray() ?? Array.Empty<string>();
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
                    group.Key.ToLowerInvariant() + "id",
                    ConditionOperator.In,
                    chunk.Cast<object>().ToArray()
                );

                Entity[] records = await sdkProxy
                    .RetrieveMultipleAsync(query, requestSettings)
                    .ConfigureAwait(false);

                foreach (Entity record in records)
                {
                    result[ValidationKey(group.Key, record.Id)] = record;
                }
            }
        }

        return result;
    }

    private static string ValidationKey(string logicalName, Guid id) =>
        logicalName.ToLowerInvariant() + "|" + id.ToString("N");
}

internal sealed class ValidationTarget(
    EntityReference reference,
    object? source,
    object? target,
    Entity? savedEntity
)
{
    public EntityReference Reference { get; } = reference;

    public object? Source { get; } = source;

    public object? Target { get; } = target;

    public Entity? SavedEntity { get; } = savedEntity;
}
