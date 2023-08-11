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

using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.New;

public class FieldDrill
{
    private readonly Connection _connection;
    private readonly ILogger<FieldDrill> _logger;

    public FieldDrill(Connection connection, ILogger<FieldDrill> logger)
    {
        Guard.IsNotNull(connection);
        Guard.IsNotNull(logger);
        _connection = connection;
        _logger = logger;
    }

    public T Retrieve<T>(EntityReference obj, string path, string delimiter = ".")
    {
        string[] pathParts = path.Split(delimiter);

        return Retrieve<T>(obj, pathParts);
    }

    public T Retrieve<T>(EntityReference drillReference, params string[] pathParts)
    {
        if (Array.Exists(pathParts, string.IsNullOrEmpty))
        {
            throw new InvalidProgramException("One of path elements is null or empty.");
        }

        Guard.IsNull(drillReference);

        for (int i = 0; i < pathParts.Length; i++)
        {
            bool isLast = i == pathParts.Length - 1;
            string currentFieldName = pathParts[i];
            Entity ret = _connection.Retrieve(drillReference.LogicalName,
                drillReference.Id,
                new ColumnSet(currentFieldName));

            if (!ret.Contains(currentFieldName))
            {
                throw new InvalidProgramException("Retrieved record doesn't contain field in attributes collection.");
            }

            object retrievedField = ret[currentFieldName];

            if (isLast)
            {
                if (retrievedField is T or null)
                {
                    return (T)retrievedField;
                }

                throw new InvalidProgramException(
                    $"Retrieved field is not same type as expected one, retrieved type is {retrievedField.GetType().Name}, expected type is {typeof(T).Name}");
            }

            drillReference = retrievedField switch
            {
                EntityReference retrievedFieldEntityReference => retrievedFieldEntityReference,
                null => throw new InvalidProgramException(
                    $"Retrieved field is null but it's not last element of path, current field name {currentFieldName}"),
                _ => throw new InvalidProgramException(
                    $"Retrieved field is not {nameof(EntityReference)}, current field name {currentFieldName}, type of retrieved field {retrievedField.GetType().Name}")
            };
        }

        throw new InvalidProgramException("Unexpected state, probably a bug.");
    }

    public async Task<T> RetrieveAsync<T>(EntityReference obj, string path, string delimiter = ".")
    {
        string[] pathParts = path.Split(delimiter);

        return await RetrieveAsync<T>(obj, pathParts);
    }

    public async Task<T> RetrieveAsync<T>(EntityReference drillReference, params string[] pathParts)
    {
        if (Array.Exists(pathParts, string.IsNullOrEmpty))
        {
            throw new InvalidProgramException("One of path elements is null or empty.");
        }

        Guard.IsNull(drillReference);

        for (int i = 0; i < pathParts.Length; i++)
        {
            bool isLast = i == pathParts.Length - 1;
            string currentFieldName = pathParts[i];
            Entity ret = await _connection.RetrieveAsync(drillReference.LogicalName,
                drillReference.Id,
                new ColumnSet(currentFieldName));

            if (!ret.Contains(currentFieldName))
            {
                throw new InvalidProgramException("Retrieved record doesn't contain field in attributes collection.");
            }

            object retrievedField = ret[currentFieldName];

            if (isLast)
            {
                if (retrievedField is T or null)
                {
                    return (T)retrievedField;
                }

                throw new InvalidProgramException(
                    $"Retrieved field is not same type as expected one, retrieved type is {retrievedField.GetType().Name}, expected type is {typeof(T).Name}");
            }

            drillReference = retrievedField switch
            {
                EntityReference retrievedFieldEntityReference => retrievedFieldEntityReference,
                null => throw new InvalidProgramException(
                    $"Retrieved field is null but it's not last element of path, current field name {currentFieldName}"),
                _ => throw new InvalidProgramException(
                    $"Retrieved field is not {nameof(EntityReference)}, current field name {currentFieldName}, type of retrieved field {retrievedField.GetType().Name}")
            };
        }

        throw new InvalidProgramException("Unexpected state, probably a bug.");
    }
}
