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

internal interface IMappingStep<TSource, TTarget>
{
    int Stage { get; }

    string TargetMember { get; }

    Task ApplyAsync(
        MappingContext<TSource, TTarget> context,
        MappingExecutionState state,
        CancellationToken cancellationToken
    );
}

internal sealed class MappingStep<TSource, TTarget>(
    int stage,
    string targetMember,
    Action<MappingContext<TSource, TTarget>> apply
) : IMappingStep<TSource, TTarget>
{
    public int Stage { get; } = stage;

    public string TargetMember { get; } = targetMember;

    public Task ApplyAsync(
        MappingContext<TSource, TTarget> context,
        MappingExecutionState state,
        CancellationToken cancellationToken
    )
    {
        apply(context);
        return Task.CompletedTask;
    }
}

internal sealed class LookupMappingStep<TSource, TTarget>(
    int stage,
    string targetMember,
    Action<TTarget, EntityReference> setter,
    Func<MappingContext<TSource, TTarget>, EntityKey> keyFactory,
    Func<MappingContext<TSource, TTarget>, Entity>? createFactory,
    LookupMappingOptions options,
    LookupResolver resolver
) : IMappingStep<TSource, TTarget>
{
    public int Stage { get; } = stage;

    public string TargetMember { get; } = targetMember;

    public async Task ApplyAsync(
        MappingContext<TSource, TTarget> context,
        MappingExecutionState state,
        CancellationToken cancellationToken
    )
    {
        EntityKey key = keyFactory(context);
        if (key == null || key.IsEmpty)
        {
            string message = $"Lookup '{TargetMember}' key is empty.";
            if (options.ThrowIfEmpty)
            {
                throw new MapperException(message);
            }

            context.AddError(new MappingError(context.Source, context.Target, TargetMember, message));
            return;
        }

        EntityReference? reference = await resolver
            .ResolveAsync(
                key,
                options,
                createFactory == null ? null : () => createFactory(context),
                state,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (reference == null)
        {
            context.AddError(
                new MappingError(
                    context.Source,
                    context.Target,
                    TargetMember,
                    $"Lookup record '{key}' was not found."
                )
            );
            return;
        }

        setter(context.Target, reference);
    }
}
