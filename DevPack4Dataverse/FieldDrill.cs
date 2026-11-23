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
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse;

/// <summary>
/// Retrieves a field by following a path of <see cref="EntityReference"/> relationships.
/// </summary>
/// <remarks>
/// Each path element is resolved with a separate retrieve request. Path segments are logical
/// attribute names, and every intermediate field must contain an <see cref="EntityReference"/>.
/// The type has no mutable state; concurrent calls delegate to the supplied <see cref="SdkProxy"/>.
/// </remarks>
public sealed class FieldDrill
{
    private readonly SdkProxy _sdkProxy;

    /// <summary>
    /// Creates a drill for the supplied connection layer.
    /// </summary>
    /// <param name="sdkProxy">Connection layer used for retrieve requests. Cannot be null.</param>
    /// <param name="logger">Logger used by the drill. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public FieldDrill(SdkProxy sdkProxy, ILogger logger)
    {
        _sdkProxy = Guard.Against.Null(sdkProxy);
        Guard.Against.Null(logger);
    }

    /// <summary>
    /// Retrieves a field by following a delimited path from <paramref name="obj"/>.
    /// </summary>
    /// <typeparam name="T">Expected type of the final field value.</typeparam>
    /// <param name="obj">Starting entity reference. Cannot be null.</param>
    /// <param name="path">Delimited path of logical attribute names. Cannot be null or whitespace.</param>
    /// <param name="delimiter">Path delimiter. Cannot be null or empty.</param>
    /// <returns>The value of the final field in the path.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="obj"/>, <paramref name="path"/>, or <paramref name="delimiter"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="obj"/> has no logical name or ID, the path is empty or contains empty/whitespace
    /// segments, the delimiter is empty, or an intermediate <see cref="EntityReference"/> has no logical
    /// name or ID.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A retrieved intermediate field is missing or null, an intermediate field is not an
    /// <see cref="EntityReference"/>, the final field is null or absent for a non-nullable value
    /// type, or the final field has an unexpected type.
    /// </exception>
    public T Retreive<T>(EntityReference obj, string path, string delimiter = ".")
    {
        string[] pathParts = SplitPath(path, delimiter);
        return Retreive<T>(obj, pathParts);
    }

    /// <summary>
    /// Retrieves a field by following an explicit path of logical attribute names from <paramref name="obj"/>.
    /// </summary>
    /// <typeparam name="T">Expected type of the final field value.</typeparam>
    /// <param name="obj">Starting entity reference. Cannot be null.</param>
    /// <param name="pathParts">Logical attribute names to traverse. Cannot be null or empty.</param>
    /// <returns>The value of the final field in the path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="obj"/> or <paramref name="pathParts"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="obj"/> has no logical name or ID, the path is empty or contains
    /// empty/whitespace segments, or an intermediate <see cref="EntityReference"/> has no logical
    /// name or ID.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A retrieved intermediate field is missing or null, an intermediate field is not an
    /// <see cref="EntityReference"/>, the final field is null or absent for a non-nullable value
    /// type, or the final field has an unexpected type.
    /// </exception>
    public T Retreive<T>(EntityReference obj, string[] pathParts)
    {
        string[] path = ValidateArguments(obj, pathParts);

        EntityReference drillReference = obj;
        for (int index = 0; index < path.Length; index++)
        {
            bool isLast = index == path.Length - 1;
            string currentFieldName = path[index];
            Entity retrievedRecord = Guard.Against.Null(
                _sdkProxy.Retrieve(
                    drillReference.LogicalName,
                    drillReference.Id,
                    new ColumnSet(currentFieldName)
                )
            );
            if (!retrievedRecord.Contains(currentFieldName))
            {
                if (isLast)
                {
                    // Dataverse omits attributes whose value is null. The final segment is
                    // therefore allowed to be absent and maps to null for reference types.
                    return ConvertFinalValue<T>(null, currentFieldName);
                }

                throw new InvalidOperationException(
                    $"Retrieved record does not contain field '{currentFieldName}'."
                );
            }
            object retrievedField = retrievedRecord[currentFieldName];
            if (isLast)
            {
                return ConvertFinalValue<T>(retrievedField, currentFieldName);
            }

            drillReference = GetNextReference(retrievedField, currentFieldName);
        }

        throw new InvalidOperationException("Unexpected state, probably a bug.");
    }

    /// <inheritdoc cref="Retreive{T}(EntityReference, string, string)"/>
    public async Task<T> RetreiveAsync<T>(EntityReference obj, string path, string delimiter = ".")
    {
        string[] pathParts = SplitPath(path, delimiter);
        return await RetreiveAsync<T>(obj, pathParts).ConfigureAwait(false);
    }

    /// <inheritdoc cref="Retreive{T}(EntityReference, string[])"/>
    public async Task<T> RetreiveAsync<T>(EntityReference obj, string[] pathParts)
    {
        string[] path = ValidateArguments(obj, pathParts);

        EntityReference drillReference = obj;
        for (int index = 0; index < path.Length; index++)
        {
            bool isLast = index == path.Length - 1;
            string currentFieldName = path[index];
            Entity retrievedRecord = Guard.Against.Null(
                await _sdkProxy
                    .RetrieveAsync(
                        drillReference.LogicalName,
                        drillReference.Id,
                        new ColumnSet(currentFieldName)
                    )
                    .ConfigureAwait(false)
            );
            if (!retrievedRecord.Contains(currentFieldName))
            {
                if (isLast)
                {
                    // Dataverse omits attributes whose value is null. The final segment is
                    // therefore allowed to be absent and maps to null for reference types.
                    return ConvertFinalValue<T>(null, currentFieldName);
                }

                throw new InvalidOperationException(
                    $"Retrieved record does not contain field '{currentFieldName}'."
                );
            }
            object retrievedField = retrievedRecord[currentFieldName];
            if (isLast)
            {
                return ConvertFinalValue<T>(retrievedField, currentFieldName);
            }

            drillReference = GetNextReference(retrievedField, currentFieldName);
        }

        throw new InvalidOperationException("Unexpected state, probably a bug.");
    }

    private static string[] SplitPath(string path, string delimiter)
    {
        Guard.Against.NullOrWhiteSpace(path, parameterName: nameof(path));
        Guard.Against.NullOrEmpty(delimiter, parameterName: nameof(delimiter));
        string[] parts = path.Split([delimiter], StringSplitOptions.None);
        for (int index = 0; index < parts.Length; index++)
        {
            parts[index] = parts[index].Trim();
        }

        return parts;
    }

    private static string[] ValidateArguments(EntityReference obj, string[] pathParts)
    {
        Guard.Against.Null(pathParts, parameterName: nameof(pathParts));
        bool pathIsValid = pathParts.Length > 0;
        for (int index = 0; pathIsValid && index < pathParts.Length; index++)
        {
            pathIsValid = !string.IsNullOrWhiteSpace(pathParts[index]);
        }

        Guard.Against.InvalidInput(
            pathParts,
            nameof(pathParts),
            _ => pathIsValid,
            "Path must contain at least one non-empty field name."
        );
        Guard.Against.Null(
            obj,
            message: "Drilling object cannot start with reference that is null.",
            parameterName: nameof(obj)
        );
        Guard.Against.NullOrWhiteSpace(
            obj.LogicalName,
            message: "Starting entity reference must have a logical name.",
            parameterName: nameof(obj)
        );
        Guard.Against.Default(
            obj.Id,
            message: "Starting entity reference must have a non-empty ID.",
            parameterName: nameof(obj)
        );

        string[] normalized = new string[pathParts.Length];
        for (int index = 0; index < pathParts.Length; index++)
        {
            normalized[index] = pathParts[index].Trim().ToLowerInvariant();
        }

        return normalized;
    }

    private static T ConvertFinalValue<T>(object? retrievedField, string fieldName)
    {
        if (retrievedField is T value)
        {
            return value;
        }

        if (retrievedField is null)
        {
            if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) is null)
            {
                throw new InvalidOperationException(
                    $"Retrieved field '{fieldName}' is null and cannot be returned as non-nullable type '{typeof(T).Name}'."
                );
            }

            return default!;
        }

        throw new InvalidOperationException(
            $"Retrieved field '{fieldName}' is not of the expected type. Retrieved type is {retrievedField.GetType().Name}, expected type is {typeof(T).Name}."
        );
    }

    private static EntityReference GetNextReference(object retrievedField, string fieldName)
    {
        if (retrievedField is null)
        {
            throw new InvalidOperationException(
                $"Retrieved field '{fieldName}' is null but it is not the last element of the path."
            );
        }

        if (retrievedField is EntityReference reference)
        {
            Guard.Against.NullOrWhiteSpace(
                reference.LogicalName,
                message: $"Intermediate field '{fieldName}' must have a logical name.",
                parameterName: nameof(retrievedField)
            );
            Guard.Against.Default(
                reference.Id,
                message: $"Intermediate field '{fieldName}' must have a non-empty ID.",
                parameterName: nameof(retrievedField)
            );
            return reference;
        }

        throw new InvalidOperationException(
            $"Retrieved field '{fieldName}' is not an EntityReference. Retrieved type is {retrievedField.GetType().Name}."
        );
    }
}
