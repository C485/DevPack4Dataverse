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
[EntityLogicalName("sdkmessageprocessingstepimage")]
internal partial class SdkMessageProcessingStepImage : Entity
{
    public const string EntityLogicalName = "sdkmessageprocessingstepimage";

    public const int EntityTypeCode = 4615;

    public SdkMessageProcessingStepImage() : base(EntityLogicalName) { }

    [AttributeLogicalName("attributes")]
    public string Attributes1
    {
        get => GetAttributeValue<string>("attributes");
        set { SetAttributeValue("attributes", value); }
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

    [AttributeLogicalName("description")]
    public string Description
    {
        get => GetAttributeValue<string>("description");
        set { SetAttributeValue("description", value); }
    }

    [AttributeLogicalName("entityalias")]
    public string EntityAlias
    {
        get => GetAttributeValue<string>("entityalias");
        set { SetAttributeValue("entityalias", value); }
    }

    [AttributeLogicalName("sdkmessageprocessingstepimageid")]
    public override Guid Id
    {
        get => base.Id;
        set => SdkMessageProcessingStepImageId = value;
    }

    [AttributeLogicalName("imagetype")]
    public OptionSetValue ImageType
    {
        get => GetAttributeValue<OptionSetValue>("imagetype");
        set { SetAttributeValue("imagetype", value); }
    }

    [AttributeLogicalName("introducedversion")]
    public string IntroducedVersion
    {
        get => GetAttributeValue<string>("introducedversion");
        set { SetAttributeValue("introducedversion", value); }
    }

    [AttributeLogicalName("iscustomizable")]
    public BooleanManagedProperty IsCustomizable
    {
        get => GetAttributeValue<BooleanManagedProperty>("iscustomizable");
        set { SetAttributeValue("iscustomizable", value); }
    }

    [AttributeLogicalName("ismanaged")]
    public bool? IsManaged
    {
        get => GetAttributeValue<bool?>("ismanaged");
        set { SetAttributeValue("ismanaged", value); }
    }

    [AttributeLogicalName("messagepropertyname")]
    public string MessagePropertyName
    {
        get => GetAttributeValue<string>("messagepropertyname");
        set { SetAttributeValue("messagepropertyname", value); }
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

    [AttributeLogicalName("relatedattributename")]
    public string RelatedAttributeName
    {
        get => GetAttributeValue<string>("relatedattributename");
        set { SetAttributeValue("relatedattributename", value); }
    }

    [AttributeLogicalName("sdkmessageprocessingstepid")]
    public EntityReference SdkMessageProcessingStepId
    {
        get => GetAttributeValue<EntityReference>("sdkmessageprocessingstepid");
        set { SetAttributeValue("sdkmessageprocessingstepid", value); }
    }

    [AttributeLogicalName("sdkmessageprocessingstepid_name")]
    public string SdkMessageProcessingStepIdName
    {
        get => GetAttributeValue<string>("sdkmessageprocessingstepid_name");
        set { SetAttributeValue("sdkmessageprocessingstepid_name", value); }
    }

    [AttributeLogicalName("sdkmessageprocessingstepimageid")]
    public Guid? SdkMessageProcessingStepImageId
    {
        get => GetAttributeValue<Guid?>("sdkmessageprocessingstepimageid");
        set
        {
            SetAttributeValue("sdkmessageprocessingstepimageid", value);
            base.Id = value ?? Guid.Empty;
        }
    }

    [AttributeLogicalName("sdkmessageprocessingstepimageidunique")]
    public Guid? SdkMessageProcessingStepImageIdUnique
    {
        get => GetAttributeValue<Guid?>("sdkmessageprocessingstepimageidunique");
        set { SetAttributeValue("sdkmessageprocessingstepimageidunique", value); }
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
}
