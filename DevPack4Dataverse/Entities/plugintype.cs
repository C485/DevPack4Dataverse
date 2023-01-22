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
[EntityLogicalName("plugintype")]
internal partial class PluginType : Entity
{
    public const string EntityLogicalName = "plugintype";

    public const int EntityTypeCode = 4602;

    public PluginType() : base(EntityLogicalName) { }

    [AttributeLogicalName("assemblyname")]
    public string AssemblyName
    {
        get => GetAttributeValue<string>("assemblyname");
        set { SetAttributeValue("assemblyname", value); }
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

    [AttributeLogicalName("culture")]
    public string Culture
    {
        get => GetAttributeValue<string>("culture");
        set { SetAttributeValue("culture", value); }
    }

    [AttributeLogicalName("customizationlevel")]
    public int? CustomizationLevel
    {
        get => GetAttributeValue<int?>("customizationlevel");
        set { SetAttributeValue("customizationlevel", value); }
    }

    [AttributeLogicalName("customworkflowactivityinfo")]
    public string CustomWorkflowActivityInfo
    {
        get => GetAttributeValue<string>("customworkflowactivityinfo");
        set { SetAttributeValue("customworkflowactivityinfo", value); }
    }

    [AttributeLogicalName("description")]
    public string Description
    {
        get => GetAttributeValue<string>("description");
        set { SetAttributeValue("description", value); }
    }

    [AttributeLogicalName("friendlyname")]
    public string FriendlyName
    {
        get => GetAttributeValue<string>("friendlyname");
        set { SetAttributeValue("friendlyname", value); }
    }

    [AttributeLogicalName("plugintypeid")]
    public override Guid Id
    {
        get => base.Id;
        set => PluginTypeId = value;
    }

    [AttributeLogicalName("ismanaged")]
    public bool? IsManaged
    {
        get => GetAttributeValue<bool?>("ismanaged");
        set { SetAttributeValue("ismanaged", value); }
    }

    [AttributeLogicalName("isworkflowactivity")]
    public bool? IsWorkflowActivity
    {
        get => GetAttributeValue<bool?>("isworkflowactivity");
        set { SetAttributeValue("isworkflowactivity", value); }
    }

    [AttributeLogicalName("major")]
    public int? Major
    {
        get => GetAttributeValue<int?>("major");
        set { SetAttributeValue("major", value); }
    }

    [AttributeLogicalName("minor")]
    public int? Minor
    {
        get => GetAttributeValue<int?>("minor");
        set { SetAttributeValue("minor", value); }
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

    [AttributeLogicalName("pluginassemblyid")]
    public EntityReference PluginAssemblyId
    {
        get => GetAttributeValue<EntityReference>("pluginassemblyid");
        set { SetAttributeValue("pluginassemblyid", value); }
    }

    [AttributeLogicalName("pluginassemblyid_name")]
    public string PluginAssemblyIdName
    {
        get => GetAttributeValue<string>("pluginassemblyid_name");
        set { SetAttributeValue("pluginassemblyid_name", value); }
    }

    [AttributeLogicalName("plugintypeid")]
    public Guid? PluginTypeId
    {
        get => GetAttributeValue<Guid?>("plugintypeid");
        set
        {
            SetAttributeValue("plugintypeid", value);
            base.Id = value ?? Guid.Empty;
        }
    }

    [AttributeLogicalName("plugintypeidunique")]
    public Guid? PluginTypeIdUnique
    {
        get => GetAttributeValue<Guid?>("plugintypeidunique");
        set { SetAttributeValue("plugintypeidunique", value); }
    }

    [AttributeLogicalName("publickeytoken")]
    public string PublicKeyToken
    {
        get => GetAttributeValue<string>("publickeytoken");
        set { SetAttributeValue("publickeytoken", value); }
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

    [AttributeLogicalName("typename")]
    public string TypeName
    {
        get => GetAttributeValue<string>("typename");
        set { SetAttributeValue("typename", value); }
    }

    [AttributeLogicalName("version")]
    public string Version
    {
        get => GetAttributeValue<string>("version");
        set { SetAttributeValue("version", value); }
    }

    [AttributeLogicalName("versionnumber")]
    public long? VersionNumber
    {
        get => GetAttributeValue<long?>("versionnumber");
        set { SetAttributeValue("versionnumber", value); }
    }

    [AttributeLogicalName("workflowactivitygroupname")]
    public string WorkflowActivityGroupName
    {
        get => GetAttributeValue<string>("workflowactivitygroupname");
        set { SetAttributeValue("workflowactivitygroupname", value); }
    }
}
