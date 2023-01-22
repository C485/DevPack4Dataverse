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
internal enum PluginPackageState
{
    [EnumMember]
    Active = 0,

    [EnumMember]
    Inactive = 1,
}

[DataContract]
[EntityLogicalName("pluginpackage")]
internal partial class PluginPackage : Entity
{
    public const string EntityLogicalName = "pluginpackage";

    public const int EntityTypeCode = 10101;

    public PluginPackage() : base(EntityLogicalName) { }

    [AttributeLogicalName("componentidunique")]
    public Guid? ComponentIdUnique
    {
        get => GetAttributeValue<Guid?>("componentidunique");
        set { SetAttributeValue("componentidunique", value); }
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

    [AttributeLogicalName("fileid")]
    public Guid? FileId
    {
        get => GetAttributeValue<Guid?>("fileid");
        set { SetAttributeValue("fileid", value); }
    }

    [AttributeLogicalName("fileid_name")]
    public string FileIdName
    {
        get => GetAttributeValue<string>("fileid_name");
        set { SetAttributeValue("fileid_name", value); }
    }

    [AttributeLogicalName("pluginpackageid")]
    public override Guid Id
    {
        get => base.Id;
        set => PluginPackageId = value;
    }

    [AttributeLogicalName("importsequencenumber")]
    public int? ImportSequenceNumber
    {
        get => GetAttributeValue<int?>("importsequencenumber");
        set { SetAttributeValue("importsequencenumber", value); }
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

    [AttributeLogicalName("overriddencreatedon")]
    public DateTime? OverriddenCreatedOn
    {
        get => GetAttributeValue<DateTime?>("overriddencreatedon");
        set { SetAttributeValue("overriddencreatedon", value); }
    }

    [AttributeLogicalName("overwritetime")]
    public DateTime? OverwriteTime
    {
        get => GetAttributeValue<DateTime?>("overwritetime");
        set { SetAttributeValue("overwritetime", value); }
    }

    [AttributeLogicalName("package")]
    public Guid? Package
    {
        get => GetAttributeValue<Guid?>("package");
        set { SetAttributeValue("package", value); }
    }

    [AttributeLogicalName("package_name")]
    public string PackageName
    {
        get => GetAttributeValue<string>("package_name");
        set { SetAttributeValue("package_name", value); }
    }

    [AttributeLogicalName("pluginpackageid")]
    public Guid? PluginPackageId
    {
        get => GetAttributeValue<Guid?>("pluginpackageid");
        set
        {
            SetAttributeValue("pluginpackageid", value);
            base.Id = value ?? Guid.Empty;
        }
    }

    [AttributeLogicalName("solutionid")]
    public Guid? SolutionId
    {
        get => GetAttributeValue<Guid?>("solutionid");
        set { SetAttributeValue("solutionid", value); }
    }

    [AttributeLogicalName("statecode")]
    public PluginPackageState? statecode
    {
        get
        {
            OptionSetValue optionSet = GetAttributeValue<OptionSetValue>("statecode");
            if (optionSet != null)
            {
                return (PluginPackageState)(Enum.ToObject(typeof(PluginPackageState), optionSet.Value));
            }
            else
            {
                return null;
            }
        }
        set
        {
            if (value == null)
            {
                SetAttributeValue("statecode", null);
            }
            else
            {
                SetAttributeValue("statecode", new OptionSetValue((int)(value)));
            }
        }
    }

    [AttributeLogicalName("statuscode")]
    public OptionSetValue statuscode
    {
        get => GetAttributeValue<OptionSetValue>("statuscode");
        set { SetAttributeValue("statuscode", value); }
    }

    [AttributeLogicalName("supportingsolutionid")]
    public Guid? SupportingSolutionId
    {
        get => GetAttributeValue<Guid?>("supportingsolutionid");
        set { SetAttributeValue("supportingsolutionid", value); }
    }

    [AttributeLogicalName("timezoneruleversionnumber")]
    public int? TimeZoneRuleVersionNumber
    {
        get => GetAttributeValue<int?>("timezoneruleversionnumber");
        set { SetAttributeValue("timezoneruleversionnumber", value); }
    }

    [AttributeLogicalName("uniquename")]
    public string UniqueName
    {
        get => GetAttributeValue<string>("uniquename");
        set { SetAttributeValue("uniquename", value); }
    }

    [AttributeLogicalName("utcconversiontimezonecode")]
    public int? UTCConversionTimeZoneCode
    {
        get => GetAttributeValue<int?>("utcconversiontimezonecode");
        set { SetAttributeValue("utcconversiontimezonecode", value); }
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
