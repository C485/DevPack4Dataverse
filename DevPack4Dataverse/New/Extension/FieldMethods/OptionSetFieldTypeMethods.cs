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
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace DevPack4Dataverse.New.Extension.FieldMethods;

public sealed class OptionSetFieldTypeMethods
{
    private const int NoLanguageFilter = -1;
    private readonly ServiceClient _serviceClient;

    public OptionSetFieldTypeMethods(ServiceClient serviceClient)
    {
        Guard.IsNotNull(serviceClient);
        _serviceClient = serviceClient;
    }

    public async Task<string> MapOptionSetToString(
        string logicalName,
        string fieldName,
        OptionSetValue valueToMap,
        int languageCode = NoLanguageFilter)
    {
        Guard.IsNotNull(valueToMap);
        Guard.IsNotNullOrEmpty(fieldName);
        Guard.IsNotNullOrEmpty(logicalName);

        return await MapOptionSetToString(logicalName, fieldName, valueToMap.Value, languageCode);
    }

    public async Task<string> MapOptionSetToString(
        string logicalName,
        string fieldName,
        bool valueToMap,
        int languageCode = NoLanguageFilter)
    {
        Guard.IsNotNullOrEmpty(fieldName);
        Guard.IsNotNullOrEmpty(logicalName);

        return await MapOptionSetToString(logicalName, fieldName, Convert.ToInt32(valueToMap), languageCode);
    }

    public async Task<string> MapOptionSetToString<T>(
        string logicalName,
        string fieldName,
        T valueToMap,
        int languageCode = NoLanguageFilter) where T : struct, Enum
    {
        Guard.IsNotNull(valueToMap);
        Guard.IsNotNullOrEmpty(fieldName);
        Guard.IsNotNullOrEmpty(logicalName);

        return await MapOptionSetToString(logicalName, fieldName, Convert.ToInt32(valueToMap), languageCode);
    }

    /// <summary>
    ///     It'll map int value to optionset label. Language code can be provided to get exact label for language.
    /// </summary>
    /// <param name="logicalName">Required.</param>
    /// <param name="fieldName">Required.</param>
    /// <param name="valueToMap">Value of optionset as <see cref="int" />. Required.</param>
    /// <param name="languageCode">
    ///     Optional, by default UserLocalizedLabel will be used, otherwise LocalizedLabels will be
    ///     searched for label.
    /// </param>
    /// <returns>Returns label of value.</returns>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task<string> MapOptionSetToString(
        string logicalName,
        string fieldName,
        int valueToMap,
        int languageCode = NoLanguageFilter)
    {
        Guard.IsGreaterThan(valueToMap, -1);
        Guard.IsNotNullOrEmpty(fieldName);
        Guard.IsNotNullOrEmpty(logicalName);
        OptionMetadataCollection metadataResponse = await DownloadMetadataForField(logicalName, fieldName);

        OptionMetadata mappedFieldValueMetadata = metadataResponse.FirstOrDefault(p => p.Value == valueToMap);

        if (mappedFieldValueMetadata is null)
        {
            throw new InvalidProgramException(
                $"Metadata for field was valid but option-set value of {valueToMap} was not found.");
        }

        if (languageCode == NoLanguageFilter)
        {
            return mappedFieldValueMetadata.Label.UserLocalizedLabel.Label;
        }

        LocalizedLabel languageDependentLabel =
            mappedFieldValueMetadata.Label.LocalizedLabels.SingleOrDefault(p => p.LanguageCode == languageCode);

        if (languageDependentLabel is null)
        {
            throw new InvalidProgramException($"Unable to find label for language {languageCode}.");
        }

        return languageDependentLabel
           .Label;
    }

    /// <summary>
    ///     Utilizes SDK functionality called FormattedValues that contains label for option-set in users language.
    ///     <para />
    ///     Type of field is not checked, cache is not used.
    /// </summary>
    /// <param name="sourceRecord">Required.</param>
    /// <param name="fieldName">Required.</param>
    /// <returns>Label for field.</returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public string MapOptionSetToStringUsingFormattedValues(Entity sourceRecord, string fieldName)
    {
        Guard.IsNotNull(sourceRecord);
        Guard.IsNotDefault(sourceRecord.Id);
        Guard.IsNotNullOrEmpty(sourceRecord.LogicalName);
        Guard.IsNotNullOrEmpty(fieldName);

        if (sourceRecord.FormattedValues.Contains(fieldName))
        {
            return sourceRecord.FormattedValues[fieldName];
        }

        throw new KeyNotFoundException(
            $"Formatted value for field[{fieldName}] was not found, check image/query settings.");
    }

    public async Task<T> MapStringToEnum<T>(
        string logicalName,
        string fieldName,
        string valueToMap,
        int languageCode = NoLanguageFilter,
        bool compareIgnoreCase = true) where T : struct, Enum
    {
        Guard.IsNotNullOrEmpty(valueToMap);
        Guard.IsNotNullOrEmpty(fieldName);
        Guard.IsNotNullOrEmpty(logicalName);
        OptionSetValue mapped = await MapStringToOptionSet(logicalName,
            fieldName,
            valueToMap,
            languageCode,
            compareIgnoreCase);

        Guard.IsNotNull(mapped);

        if (Enum.IsDefined(typeof(T), mapped.Value))
        {
            return (T)(object)mapped.Value;
        }

        throw new InvalidProgramException(
            $"Unable to find a mapping for table {logicalName}, field {fieldName} and value {valueToMap}");
    }

    public async Task<OptionSetValue> MapStringToOptionSet(
        string logicalName,
        string fieldName,
        string valueToMap,
        int languageCode = NoLanguageFilter,
        bool compareIgnoreCase = true)
    {
        Guard.IsNotNullOrEmpty(valueToMap);
        Guard.IsNotNullOrEmpty(fieldName);
        Guard.IsNotNullOrEmpty(logicalName);

        OptionMetadataCollection metadataResponse = await DownloadMetadataForField(logicalName, fieldName);

        if (languageCode == NoLanguageFilter)
        {
            OptionMetadata mappedFieldValueMetadata = metadataResponse.FirstOrDefault(p =>
                string.Equals(valueToMap,
                    p.Label.UserLocalizedLabel.Label,
                    compareIgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

            if (mappedFieldValueMetadata is null)
            {
                throw new InvalidProgramException(
                    $"Metadata for field was valid but option-set value of {valueToMap} was not found.");
            }

            if (mappedFieldValueMetadata.Value is null)
            {
                throw new InvalidProgramException(
                    $"Metadata for field was valid, option-set label {valueToMap} was found but its value is null.");
            }

            return new OptionSetValue(mappedFieldValueMetadata.Value.Value);
        }

        foreach (OptionMetadata optionMetadata in metadataResponse)
        {
            if (!optionMetadata.Label.LocalizedLabels.Any(label => label.LanguageCode == languageCode
                && string.Equals(valueToMap,
                    label.Label,
                    compareIgnoreCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (optionMetadata.Value is null)
            {
                throw new InvalidProgramException(
                    $"Metadata for field was valid, option-set label {valueToMap} was found but its value is null.");
            }

            return new OptionSetValue(optionMetadata.Value.Value);
        }

        throw new KeyNotFoundException(
            $"Metadata for field was valid but option-set value of {valueToMap} was not found.");
    }

    private async Task<OptionMetadataCollection> DownloadMetadataForField(string logicalName, string fieldName)
    {
        Guard.IsNotNullOrEmpty(logicalName);
        Guard.IsNotNullOrEmpty(fieldName);
        RetrieveAttributeResponse metadataInfo = await _serviceClient.Extension()
           .ExecuteAsync<RetrieveAttributeResponse>(new RetrieveAttributeRequest
            {
                EntityLogicalName = logicalName,
                LogicalName = fieldName,
                RetrieveAsIfPublished = true
            });

        if (metadataInfo?.AttributeMetadata == null)
        {
            return new OptionMetadataCollection();
        }

        Guard.IsNotNull(metadataInfo.AttributeMetadata.AttributeType);

        if (metadataInfo.AttributeMetadata.AttributeType.Value
            is AttributeTypeCode.State
            or AttributeTypeCode.Status
            or AttributeTypeCode.Picklist
            or AttributeTypeCode.Boolean)
        {
            return metadataInfo.AttributeMetadata switch
            {
                StatusAttributeMetadata statusAttributeMetadata => statusAttributeMetadata.OptionSet.Options,
                StateAttributeMetadata stateAttributeMetadata => stateAttributeMetadata.OptionSet.Options,
                PicklistAttributeMetadata picklistAttributeMetadata => picklistAttributeMetadata.OptionSet.Options,
                BooleanAttributeMetadata booleanAttributeMetadata
                    => new OptionMetadataCollection(new[]
                    {
                        booleanAttributeMetadata.OptionSet.TrueOption,
                        booleanAttributeMetadata.OptionSet.FalseOption
                    }),
                EnumAttributeMetadata enumAttributeMetadata => enumAttributeMetadata.OptionSet.Options,
                _
                    => throw new InvalidCastException(
                        $"Field[{fieldName}] in table {logicalName} is not a valid option set, enum, boolean or state/status field.")
            };
        }

        throw new InvalidProgramException(
            $"Field[{fieldName}] in table {logicalName} is not a valid option-set or state/status field.");
    }
}
