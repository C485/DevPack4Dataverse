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
[EntityLogicalName("sdkmessage")]
internal partial class SdkMessage : Entity
{
    public const string EntityLogicalName = "sdkmessage";

    public const int EntityTypeCode = 4606;

    public SdkMessage() : base(EntityLogicalName) { }

    [AttributeLogicalName("autotransact")]
    public bool? AutoTransact
    {
        get => GetAttributeValue<bool?>("autotransact");
        set { SetAttributeValue("autotransact", value); }
    }

    [AttributeLogicalName("availability")]
    public int? Availability
    {
        get => GetAttributeValue<int?>("availability");
        set { SetAttributeValue("availability", value); }
    }

    [AttributeLogicalName("categoryname")]
    public string CategoryName
    {
        get => GetAttributeValue<string>("categoryname");
        set { SetAttributeValue("categoryname", value); }
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

    [AttributeLogicalName("executeprivilegename")]
    public string ExecutePrivilegeName
    {
        get => GetAttributeValue<string>("executeprivilegename");
        set { SetAttributeValue("executeprivilegename", value); }
    }

    [AttributeLogicalName("expand")]
    public bool? Expand
    {
        get => GetAttributeValue<bool?>("expand");
        set { SetAttributeValue("expand", value); }
    }

    [AttributeLogicalName("sdkmessageid")]
    public override Guid Id
    {
        get => base.Id;
        set => SdkMessageId = value;
    }

    [AttributeLogicalName("introducedversion")]
    public string IntroducedVersion
    {
        get => GetAttributeValue<string>("introducedversion");
        set { SetAttributeValue("introducedversion", value); }
    }

    [AttributeLogicalName("isactive")]
    public bool? IsActive
    {
        get => GetAttributeValue<bool?>("isactive");
        set { SetAttributeValue("isactive", value); }
    }

    [AttributeLogicalName("ismanaged")]
    public bool? IsManaged
    {
        get => GetAttributeValue<bool?>("ismanaged");
        set { SetAttributeValue("ismanaged", value); }
    }

    [AttributeLogicalName("isprivate")]
    public bool? IsPrivate
    {
        get => GetAttributeValue<bool?>("isprivate");
        set { SetAttributeValue("isprivate", value); }
    }

    [AttributeLogicalName("isreadonly")]
    public bool? IsReadOnly
    {
        get => GetAttributeValue<bool?>("isreadonly");
        set { SetAttributeValue("isreadonly", value); }
    }

    [AttributeLogicalName("isvalidforexecuteasync")]
    public bool? IsValidForExecuteAsync
    {
        get => GetAttributeValue<bool?>("isvalidforexecuteasync");
        set { SetAttributeValue("isvalidforexecuteasync", value); }
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

    [AttributeLogicalName("sdkmessageid")]
    public Guid? SdkMessageId
    {
        get => GetAttributeValue<Guid?>("sdkmessageid");
        set
        {
            SetAttributeValue("sdkmessageid", value);
            base.Id = value ?? Guid.Empty;
        }
    }

    [AttributeLogicalName("sdkmessageidunique")]
    public Guid? SdkMessageIdUnique
    {
        get => GetAttributeValue<Guid?>("sdkmessageidunique");
        set { SetAttributeValue("sdkmessageidunique", value); }
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

    [AttributeLogicalName("template")]
    public bool? Template
    {
        get => GetAttributeValue<bool?>("template");
        set { SetAttributeValue("template", value); }
    }

    [AttributeLogicalName("throttlesettings")]
    public string ThrottleSettings
    {
        get => GetAttributeValue<string>("throttlesettings");
        set { SetAttributeValue("throttlesettings", value); }
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
