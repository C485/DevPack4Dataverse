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

using System.Runtime.Serialization;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

namespace DevPack4Dataverse.Entities;

[DataContract]
[EntityLogicalName("sdkmessagefilter")]
internal partial class SdkMessageFilter : Entity
{
    public const string EntityLogicalName = "sdkmessagefilter";

    public const int EntityTypeCode = 4607;

    public SdkMessageFilter() : base(EntityLogicalName) { }

    [AttributeLogicalName("availability")]
    public int? Availability
    {
        get => GetAttributeValue<int?>("availability");
        set { SetAttributeValue("availability", value); }
    }

    [AttributeLogicalName("componentstate")]
    public OptionSetValue ComponentState
    {
        get => GetAttributeValue<OptionSetValue>("componentstate");
        set { SetAttributeValue("componentstate", value); }
    }

    [AttributeLogicalName("createdby")]
    public EntityReference CreatedBy
    {
        get => GetAttributeValue<EntityReference>("createdby");
        set { SetAttributeValue("createdby", value); }
    }

    [AttributeLogicalName("createdby_name")]
    public string CreatedByName
    {
        get => GetAttributeValue<string>("createdby_name");
        set { SetAttributeValue("createdby_name", value); }
    }

    [AttributeLogicalName("createdon")]
    public DateTime? CreatedOn
    {
        get => GetAttributeValue<DateTime?>("createdon");
        set { SetAttributeValue("createdon", value); }
    }

    [AttributeLogicalName("createdonbehalfby")]
    public EntityReference CreatedOnBehalfBy
    {
        get => GetAttributeValue<EntityReference>("createdonbehalfby");
        set { SetAttributeValue("createdonbehalfby", value); }
    }

    [AttributeLogicalName("createdonbehalfby_name")]
    public string CreatedOnBehalfByName
    {
        get => GetAttributeValue<string>("createdonbehalfby_name");
        set { SetAttributeValue("createdonbehalfby_name", value); }
    }

    [AttributeLogicalName("customizationlevel")]
    public int? CustomizationLevel
    {
        get => GetAttributeValue<int?>("customizationlevel");
        set { SetAttributeValue("customizationlevel", value); }
    }

    [AttributeLogicalName("sdkmessagefilterid")]
    public override Guid Id
    {
        get => base.Id;
        set => SdkMessageFilterId = value;
    }

    [AttributeLogicalName("introducedversion")]
    public string IntroducedVersion
    {
        get => GetAttributeValue<string>("introducedversion");
        set { SetAttributeValue("introducedversion", value); }
    }

    [AttributeLogicalName("iscustomprocessingstepallowed")]
    public bool? IsCustomProcessingStepAllowed
    {
        get => GetAttributeValue<bool?>("iscustomprocessingstepallowed");
        set { SetAttributeValue("iscustomprocessingstepallowed", value); }
    }

    [AttributeLogicalName("ismanaged")]
    public bool? IsManaged
    {
        get => GetAttributeValue<bool?>("ismanaged");
        set { SetAttributeValue("ismanaged", value); }
    }

    [AttributeLogicalName("isvisible")]
    public bool? IsVisible
    {
        get => GetAttributeValue<bool?>("isvisible");
        set { SetAttributeValue("isvisible", value); }
    }

    [AttributeLogicalName("modifiedby")]
    public EntityReference ModifiedBy
    {
        get => GetAttributeValue<EntityReference>("modifiedby");
        set { SetAttributeValue("modifiedby", value); }
    }

    [AttributeLogicalName("modifiedby_name")]
    public string ModifiedByName
    {
        get => GetAttributeValue<string>("modifiedby_name");
        set { SetAttributeValue("modifiedby_name", value); }
    }

    [AttributeLogicalName("modifiedon")]
    public DateTime? ModifiedOn
    {
        get => GetAttributeValue<DateTime?>("modifiedon");
        set { SetAttributeValue("modifiedon", value); }
    }

    [AttributeLogicalName("modifiedonbehalfby")]
    public EntityReference ModifiedOnBehalfBy
    {
        get => GetAttributeValue<EntityReference>("modifiedonbehalfby");
        set { SetAttributeValue("modifiedonbehalfby", value); }
    }

    [AttributeLogicalName("modifiedonbehalfby_name")]
    public string ModifiedOnBehalfByName
    {
        get => GetAttributeValue<string>("modifiedonbehalfby_name");
        set { SetAttributeValue("modifiedonbehalfby_name", value); }
    }

    [AttributeLogicalName("name")]
    public string Name
    {
        get => GetAttributeValue<string>("name");
        set { SetAttributeValue("name", value); }
    }

    [AttributeLogicalName("organizationid")]
    public EntityReference OrganizationId
    {
        get => GetAttributeValue<EntityReference>("organizationid");
        set { SetAttributeValue("organizationid", value); }
    }

    [AttributeLogicalName("organizationid_name")]
    public string OrganizationIdName
    {
        get => GetAttributeValue<string>("organizationid_name");
        set { SetAttributeValue("organizationid_name", value); }
    }

    [AttributeLogicalName("overwritetime")]
    public DateTime? OverwriteTime
    {
        get => GetAttributeValue<DateTime?>("overwritetime");
        set { SetAttributeValue("overwritetime", value); }
    }

    [AttributeLogicalName("primaryobjecttypecode")]
    public string PrimaryObjectTypeCode
    {
        get => GetAttributeValue<string>("primaryobjecttypecode");
        set { SetAttributeValue("primaryobjecttypecode", value); }
    }

    [AttributeLogicalName("restrictionlevel")]
    public int? RestrictionLevel
    {
        get => GetAttributeValue<int?>("restrictionlevel");
        set { SetAttributeValue("restrictionlevel", value); }
    }

    [AttributeLogicalName("sdkmessagefilterid")]
    public Guid? SdkMessageFilterId
    {
        get => GetAttributeValue<Guid?>("sdkmessagefilterid");
        set
        {
            SetAttributeValue("sdkmessagefilterid", value);
            base.Id = value ?? Guid.Empty;
        }
    }

    [AttributeLogicalName("sdkmessagefilteridunique")]
    public Guid? SdkMessageFilterIdUnique
    {
        get => GetAttributeValue<Guid?>("sdkmessagefilteridunique");
        set { SetAttributeValue("sdkmessagefilteridunique", value); }
    }

    [AttributeLogicalName("sdkmessageid")]
    public EntityReference SdkMessageId
    {
        get => GetAttributeValue<EntityReference>("sdkmessageid");
        set { SetAttributeValue("sdkmessageid", value); }
    }

    [AttributeLogicalName("sdkmessageid_name")]
    public string SdkMessageIdName
    {
        get => GetAttributeValue<string>("sdkmessageid_name");
        set { SetAttributeValue("sdkmessageid_name", value); }
    }

    [AttributeLogicalName("secondaryobjecttypecode")]
    public string SecondaryObjectTypeCode
    {
        get => GetAttributeValue<string>("secondaryobjecttypecode");
        set { SetAttributeValue("secondaryobjecttypecode", value); }
    }

    [AttributeLogicalName("solutionid")]
    public Guid? SolutionId
    {
        get => GetAttributeValue<Guid?>("solutionid");
        set { SetAttributeValue("solutionid", value); }
    }

    [AttributeLogicalName("supportingsolutionid")]
    public Guid? SupportingSolutionId
    {
        get => GetAttributeValue<Guid?>("supportingsolutionid");
        set { SetAttributeValue("supportingsolutionid", value); }
    }

    [AttributeLogicalName("versionnumber")]
    public long? VersionNumber
    {
        get => GetAttributeValue<long?>("versionnumber");
        set { SetAttributeValue("versionnumber", value); }
    }

    [AttributeLogicalName("workflowsdkstepenabled")]
    public bool? WorkflowSdkStepEnabled
    {
        get => GetAttributeValue<bool?>("workflowsdkstepenabled");
        set { SetAttributeValue("workflowsdkstepenabled", value); }
    }
}
