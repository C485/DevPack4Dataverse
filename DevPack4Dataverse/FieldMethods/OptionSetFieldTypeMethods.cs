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
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace DevPack4Dataverse.FieldMethods;

public sealed class OptionSetFieldTypeMethods
{
    private const int NoLanguageFilter = -1;
    private readonly ILogger _logger;
    private readonly SdkProxy _sdkProxy;

    public OptionSetFieldTypeMethods(SdkProxy sdkProxy, ILogger logger)
    {
        using EntryExitLogger logGuard = new(logger);

        _logger = Guard.Against.Null(logger);

        _sdkProxy = Guard.Against.Null(sdkProxy);
    }

    public async Task<string> MapOptionSetToString(
        string logicalName,
        string fieldName,
        OptionSetValue valueToMap,
        int languageCode = NoLanguageFilter
    )
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(valueToMap);
        return await MapOptionSetToString(logicalName, fieldName, valueToMap.Value, languageCode);
    }

    public async Task<string> MapOptionSetToString(
        string logicalName,
        string fieldName,
        bool valueToMap,
        int languageCode = NoLanguageFilter
    )
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(valueToMap);
        return await MapOptionSetToString(logicalName, fieldName, Convert.ToInt32(valueToMap), languageCode);
    }

    public async Task<string> MapOptionSetToString<T>(
        string logicalName,
        string fieldName,
        T valueToMap,
        int languageCode = NoLanguageFilter
    ) where T : struct, Enum
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(valueToMap);
        return await MapOptionSetToString(logicalName, fieldName, Convert.ToInt32(valueToMap), languageCode);
    }

    /// <summary>
    /// It'll map int value to optionset label. Language code can be provided to get exact label for language.
    /// </summary>
    /// <param name="logicalName">Required.</param>
    /// <param name="fieldName">Required.</param>
    /// <param name="valueToMap">Value of optionset as <see cref="int"/>. Required.</param>
    /// <param name="languageCode">Optional, by default UserLocalizedLabel will be used, otherwise LocalizedLabels will be searched for label.</param>
    /// <returns>Returns label of value.</returns>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task<string> MapOptionSetToString(
        string logicalName,
        string fieldName,
        int valueToMap,
        int languageCode = NoLanguageFilter
    )
    {
        using EntryExitLogger logGuard = new(_logger);

        OptionMetadataCollection metadataResponse = await DownloadMetadataForField(logicalName, fieldName);

        OptionMetadata mappedFieldValueMetadata = metadataResponse.FirstOrDefault(p => p.Value == valueToMap);
        Guard.Against.Null(
            mappedFieldValueMetadata,
            message: $"Metadata for field was valid but optionset value of {valueToMap} was not found."
        );
        if (languageCode == NoLanguageFilter)
        {
            return mappedFieldValueMetadata.Label.UserLocalizedLabel.Label;
        }
        LocalizedLabel languageDependentLabel = mappedFieldValueMetadata.Label.LocalizedLabels.SingleOrDefault(
            p => p.LanguageCode == languageCode
        );
        return Guard.Against
            .Null(languageDependentLabel, message: $"Unable to find label for language {languageCode}.")
            .Label;
    }

    /// <summary>
    /// Utilizes SDK functionality called FormattedValues that contains label for optionset in users language. <para />
    /// Type of field is not checked, cache is not used.
    /// </summary>
    /// <param name="sourceRecord">Required.</param>
    /// <param name="fieldName">Required.</param>
    /// <returns>Label for field.</returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public string MapOptionSetToStringUsingFormatedValues(Entity sourceRecord, string fieldName)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(sourceRecord);
        Guard.Against.NullOrEmpty(fieldName);
        if (sourceRecord.FormattedValues.Contains(fieldName))
        {
            return sourceRecord.FormattedValues[fieldName];
        }
        throw new KeyNotFoundException(
            $"Formated value for field[{fieldName}] was not found, check image/query settings."
        );
    }

    public async Task<T> MapStringToEnum<T>(
        string logicalName,
        string fieldName,
        string valueToMap,
        int languageCode = NoLanguageFilter,
        bool compareIgnoreCase = true
    ) where T : struct, Enum
    {
        using EntryExitLogger logGuard = new(_logger);

        OptionSetValue mapped = await MapStringToOptionSet(
            logicalName,
            fieldName,
            valueToMap,
            languageCode,
            compareIgnoreCase
        );

        return (T)(object)Guard.Against.EnumOutOfRange<T>(Guard.Against.Null(mapped).Value);
    }

    public async Task<OptionSetValue> MapStringToOptionSet(
        string logicalName,
        string fieldName,
        string valueToMap,
        int languageCode = NoLanguageFilter,
        bool compareIgnoreCase = true
    )
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrEmpty(valueToMap);

        OptionMetadataCollection metadataResponse = await DownloadMetadataForField(logicalName, fieldName);

        if (languageCode == NoLanguageFilter)
        {
            OptionMetadata mappedFieldValueMetadata = metadataResponse.FirstOrDefault(
                p =>
                    string.Equals(
                        valueToMap,
                        p.Label.UserLocalizedLabel.Label,
                        compareIgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
                    )
            );
            Guard.Against.Null(
                mappedFieldValueMetadata,
                message: $"Metadata for field was valid but optionset value of {valueToMap} was not found."
            );
            Guard.Against.Null(
                mappedFieldValueMetadata.Value,
                message: $"Metadata for field was valid, optionset label {valueToMap} was found but its value is null."
            );
            return new OptionSetValue(mappedFieldValueMetadata.Value.Value);
        }

        foreach (OptionMetadata optionMetadata in metadataResponse)
        {
            foreach (LocalizedLabel label in optionMetadata.Label.LocalizedLabels)
            {
                if (
                    label.LanguageCode == languageCode
                    && string.Equals(
                        valueToMap,
                        label.Label,
                        compareIgnoreCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    Guard.Against.Null(
                        optionMetadata.Value,
                        message: $"Metadata for field was valid, optionset label {valueToMap} was found but its value is null."
                    );
                    return new OptionSetValue(optionMetadata.Value.Value);
                }
            }
        }

        throw new KeyNotFoundException(
            $"Metadata for field was valid but optionset value of {valueToMap} was not found."
        );
    }

    private async Task<OptionMetadataCollection> DownloadMetadataForField(string logicalName, string fieldName)
    {
        using EntryExitLogger logGuard = new(_logger);

        RetrieveAttributeResponse metadataInfo = await _sdkProxy.ExecuteAsync<RetrieveAttributeResponse>(
            new RetrieveAttributeRequest
            {
                EntityLogicalName = logicalName,
                LogicalName = fieldName,
                RetrieveAsIfPublished = true
            }
        );
        if (metadataInfo == null || metadataInfo.AttributeMetadata == null)
        {
            return new OptionMetadataCollection();
        }
        Guard.Against.Null(metadataInfo.AttributeMetadata.AttributeType);
        Guard.Against.AgainstExpression(
            (attributeType) =>
            {
                return attributeType
                    is AttributeTypeCode.State
                        or AttributeTypeCode.Status
                        or AttributeTypeCode.Picklist
                        or AttributeTypeCode.Boolean;
            },
            metadataInfo.AttributeMetadata.AttributeType.Value,
            $"Field[{fieldName}] in table {logicalName} is not a valid optionset or state/status field."
        );
        return metadataInfo.AttributeMetadata switch
        {
            StatusAttributeMetadata statusAttributeMetadata => statusAttributeMetadata.OptionSet.Options,
            StateAttributeMetadata stateAttributeMetadata => stateAttributeMetadata.OptionSet.Options,
            PicklistAttributeMetadata picklistAttributeMetadata => picklistAttributeMetadata.OptionSet.Options,
            BooleanAttributeMetadata booleanAttributeMetadata
                => new OptionMetadataCollection(
                    new OptionMetadata[]
                    {
                        booleanAttributeMetadata.OptionSet.TrueOption,
                        booleanAttributeMetadata.OptionSet.FalseOption
                    }
                ),
            EnumAttributeMetadata enumAttributeMetadata => enumAttributeMetadata.OptionSet.Options,
            _
                => throw new InvalidCastException(
                    $"Field[{fieldName}] in table {logicalName} is not a valid option set, enum, boolean or state/status field."
                ),
        };
    }
}
