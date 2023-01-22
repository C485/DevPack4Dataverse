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
[EntityLogicalName("pluginassembly")]
internal partial class PluginAssembly : Entity
{
    public const string EntityLogicalName = "pluginassembly";

    public const int EntityTypeCode = 4605;

    public PluginAssembly() : base(EntityLogicalName) { }

    [AttributeLogicalName("authtype")]
    public OptionSetValue AuthType
    {
        get => GetAttributeValue<OptionSetValue>("authtype");
        set { SetAttributeValue("authtype", value); }
    }

    [AttributeLogicalName("componentstate")]
    public OptionSetValue ComponentState
    {
        get => GetAttributeValue<OptionSetValue>("componentstate");
        set { SetAttributeValue("componentstate", value); }
    }

    [AttributeLogicalName("content")]
    public string Content
    {
        get => GetAttributeValue<string>("content");
        set { SetAttributeValue("content", value); }
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

    [AttributeLogicalName("description")]
    public string Description
    {
        get => GetAttributeValue<string>("description");
        set { SetAttributeValue("description", value); }
    }

    [AttributeLogicalName("pluginassemblyid")]
    public override Guid Id
    {
        get => base.Id;
        set => PluginAssemblyId = value;
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

    [AttributeLogicalName("ishidden")]
    public BooleanManagedProperty IsHidden
    {
        get => GetAttributeValue<BooleanManagedProperty>("ishidden");
        set { SetAttributeValue("ishidden", value); }
    }

    [AttributeLogicalName("ismanaged")]
    public bool? IsManaged
    {
        get => GetAttributeValue<bool?>("ismanaged");
        set { SetAttributeValue("ismanaged", value); }
    }

    [AttributeLogicalName("isolationmode")]
    public OptionSetValue IsolationMode
    {
        get => GetAttributeValue<OptionSetValue>("isolationmode");
        set { SetAttributeValue("isolationmode", value); }
    }

    [AttributeLogicalName("ispasswordset")]
    public bool? IsPasswordSet
    {
        get => GetAttributeValue<bool?>("ispasswordset");
        set { SetAttributeValue("ispasswordset", value); }
    }

    [AttributeLogicalName("major")]
    public int? Major
    {
        get => GetAttributeValue<int?>("major");
        set { SetAttributeValue("major", value); }
    }

    [AttributeLogicalName("managedidentityid")]
    public EntityReference ManagedIdentityId
    {
        get => GetAttributeValue<EntityReference>("managedidentityid");
        set { SetAttributeValue("managedidentityid", value); }
    }

    [AttributeLogicalName("managedidentityid_name")]
    public string ManagedIdentityIdName
    {
        get => GetAttributeValue<string>("managedidentityid_name");
        set { SetAttributeValue("managedidentityid_name", value); }
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

    [AttributeLogicalName("packageid")]
    public EntityReference PackageId
    {
        get => GetAttributeValue<EntityReference>("packageid");
        set { SetAttributeValue("packageid", value); }
    }

    [AttributeLogicalName("packageid_name")]
    public string PackageIdName
    {
        get => GetAttributeValue<string>("packageid_name");
        set { SetAttributeValue("packageid_name", value); }
    }

    [AttributeLogicalName("password")]
    public string Password
    {
        get => GetAttributeValue<string>("password");
        set { SetAttributeValue("password", value); }
    }

    [AttributeLogicalName("path")]
    public string Path
    {
        get => GetAttributeValue<string>("path");
        set { SetAttributeValue("path", value); }
    }

    [AttributeLogicalName("pluginassemblyid")]
    public Guid? PluginAssemblyId
    {
        get => GetAttributeValue<Guid?>("pluginassemblyid");
        set
        {
            SetAttributeValue("pluginassemblyid", value);
            base.Id = value ?? Guid.Empty;
        }
    }

    [AttributeLogicalName("pluginassemblyidunique")]
    public Guid? PluginAssemblyIdUnique
    {
        get => GetAttributeValue<Guid?>("pluginassemblyidunique");
        set { SetAttributeValue("pluginassemblyidunique", value); }
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

    [AttributeLogicalName("sourcehash")]
    public string SourceHash
    {
        get => GetAttributeValue<string>("sourcehash");
        set { SetAttributeValue("sourcehash", value); }
    }

    [AttributeLogicalName("sourcetype")]
    public OptionSetValue SourceType
    {
        get => GetAttributeValue<OptionSetValue>("sourcetype");
        set { SetAttributeValue("sourcetype", value); }
    }

    [AttributeLogicalName("supportingsolutionid")]
    public Guid? SupportingSolutionId
    {
        get => GetAttributeValue<Guid?>("supportingsolutionid");
        set { SetAttributeValue("supportingsolutionid", value); }
    }

    [AttributeLogicalName("url")]
    public string Url
    {
        get => GetAttributeValue<string>("url");
        set { SetAttributeValue("url", value); }
    }

    [AttributeLogicalName("username")]
    public string UserName
    {
        get => GetAttributeValue<string>("username");
        set { SetAttributeValue("username", value); }
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
}
