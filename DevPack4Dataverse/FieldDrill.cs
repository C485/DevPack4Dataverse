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
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse
{
    public class FieldDrill
    {
        private readonly ILogger _logger;
        private readonly SdkProxy _sdkProxy;

        public FieldDrill(SdkProxy sdkProxy, ILogger logger)
        {
            using EntryExitLogger logGuard = new(logger);
            _sdkProxy = Guard
                .Against
                .Null(sdkProxy);
            _logger = logger;
        }

        public T Retreive<T>(EntityReference obj, string path, string delimiter = ".")
        {
            using EntryExitLogger logGuard = new(_logger);
            string[] pathParts = path.Split(delimiter);
            return Retreive<T>(obj, pathParts);
        }

        public T Retreive<T>(EntityReference obj, params string[] pathParts)
        {
            using EntryExitLogger logGuard = new(_logger);
            Guard.Against.InvalidInput(pathParts, nameof(pathParts), p => p.All(u => !string.IsNullOrEmpty(u)), "One of path elements is null or empty.");
            EntityReference drillReference = Guard.Against.Null(obj, message: "Drilling object cannot start with reference that is null.");
            for (int i = 0; i < pathParts.Length; i++)
            {
                bool isLast = i == pathParts.Length - 1;
                string currentFieldName = pathParts[i];
                Entity ret = _sdkProxy.Retrieve(drillReference.LogicalName, drillReference.Id, new ColumnSet(currentFieldName));
                Guard.Against.InvalidInput(ret, nameof(ret), p => p.Contains(currentFieldName), "Retrieved record doesn't contain field in attributes collection.");
                object retrievedField = ret[currentFieldName];
                if (isLast)
                {
                    if (retrievedField is T || retrievedField is null)
                    {
                        return (T)retrievedField;
                    }
                    throw new InvalidProgramException($"Retrieved field is not same type as expected one, retrieved type is {retrievedField.GetType().Name}, expected type is {typeof(T).Name}");
                }
                if (retrievedField is EntityReference retivedFieldEntityReference)
                {
                    drillReference = retivedFieldEntityReference;
                }
                else if (retrievedField is null)
                {
                    throw new InvalidProgramException($"Retrieved field is null but it's not last element of path, current field name {currentFieldName}");
                }
                else
                {
                    throw new InvalidProgramException($"Retrieved field is not {nameof(EntityReference)}, current field name {currentFieldName}, type of retrieved field {retrievedField.GetType().Name}");
                }
            }
            throw new InvalidProgramException("Unexpected state, probably a bug.");
        }

        public async Task<T> RetreiveAsync<T>(EntityReference obj, string path, string delimiter = ".")
        {
            using EntryExitLogger logGuard = new(_logger);
            string[] pathParts = path.Split(delimiter);
            return await RetreiveAsync<T>(obj, pathParts);
        }

        public async Task<T> RetreiveAsync<T>(EntityReference obj, params string[] pathParts)
        {
            using EntryExitLogger logGuard = new(_logger);
            Guard.Against.InvalidInput(pathParts, nameof(pathParts), p => p.All(u => !string.IsNullOrEmpty(u)), "One of path elements is null or empty.");
            EntityReference drillReference = Guard.Against.Null(obj, message: "Drilling object cannot start with reference that is null.");
            for (int i = 0; i < pathParts.Length; i++)
            {
                bool isLast = i == pathParts.Length - 1;
                string currentFieldName = pathParts[i];
                Entity ret = await _sdkProxy.RetrieveAsync(drillReference.LogicalName, drillReference.Id, new ColumnSet(currentFieldName));
                Guard.Against.InvalidInput(ret, nameof(ret), p => p.Contains(currentFieldName), "Retrieved record doesn't contain field in attributes collection.");
                object retrievedField = ret[currentFieldName];
                if (isLast)
                {
                    if (retrievedField is T || retrievedField is null)
                    {
                        return (T)retrievedField;
                    }
                    throw new InvalidProgramException($"Retrieved field is not same type as expected one, retrieved type is {retrievedField.GetType().Name}, expected type is {typeof(T).Name}");
                }
                if (retrievedField is EntityReference retivedFieldEntityReference)
                {
                    drillReference = retivedFieldEntityReference;
                }
                else if (retrievedField is null)
                {
                    throw new InvalidProgramException($"Retrieved field is null but it's not last element of path, current field name {currentFieldName}");
                }
                else
                {
                    throw new InvalidProgramException($"Retrieved field is not {nameof(EntityReference)}, current field name {currentFieldName}, type of retrieved field {retrievedField.GetType().Name}");
                }
            }
            throw new InvalidProgramException("Unexpected state, probably a bug.");
        }
    }
}