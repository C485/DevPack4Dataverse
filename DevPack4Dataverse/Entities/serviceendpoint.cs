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
[EntityLogicalName("serviceendpoint")]
internal partial class ServiceEndpoint : Entity
{
    public const string EntityLogicalName = "serviceendpoint";

    public const int EntityTypeCode = 4618;

    public ServiceEndpoint() : base(EntityLogicalName) { }

    [AttributeLogicalName("authtype")]
    public OptionSetValue AuthType
    {
        get => GetAttributeValue<OptionSetValue>("authtype");
        set { SetAttributeValue("authtype", value); }
    }

    [AttributeLogicalName("authvalue")]
    public string AuthValue
    {
        get => GetAttributeValue<string>("authvalue");
        set { SetAttributeValue("authvalue", value); }
    }

    [AttributeLogicalName("componentstate")]
    public OptionSetValue ComponentState
    {
        get => GetAttributeValue<OptionSetValue>("componentstate");
        set { SetAttributeValue("componentstate", value); }
    }

    [AttributeLogicalName("connectionmode")]
    public OptionSetValue ConnectionMode
    {
        get => GetAttributeValue<OptionSetValue>("connectionmode");
        set { SetAttributeValue("connectionmode", value); }
    }

    [AttributeLogicalName("contract")]
    public OptionSetValue Contract
    {
        get => GetAttributeValue<OptionSetValue>("contract");
        set { SetAttributeValue("contract", value); }
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

    [AttributeLogicalName("description")]
    public string Description
    {
        get => GetAttributeValue<string>("description");
        set { SetAttributeValue("description", value); }
    }

    [AttributeLogicalName("serviceendpointid")]
    public override Guid Id
    {
        get => base.Id;
        set => ServiceEndpointId = value;
    }

    [AttributeLogicalName("introducedversion")]
    public string IntroducedVersion
    {
        get => GetAttributeValue<string>("introducedversion");
        set { SetAttributeValue("introducedversion", value); }
    }

    [AttributeLogicalName("isauthvalueset")]
    public bool? IsAuthValueSet
    {
        get => GetAttributeValue<bool?>("isauthvalueset");
        set { SetAttributeValue("isauthvalueset", value); }
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

    [AttributeLogicalName("issaskeyset")]
    public bool? IsSASKeySet
    {
        get => GetAttributeValue<bool?>("issaskeyset");
        set { SetAttributeValue("issaskeyset", value); }
    }

    [AttributeLogicalName("issastokenset")]
    public bool? IsSASTokenSet
    {
        get => GetAttributeValue<bool?>("issastokenset");
        set { SetAttributeValue("issastokenset", value); }
    }

    [AttributeLogicalName("keyvaultreferenceid")]
    public EntityReference KeyVaultReferenceId
    {
        get => GetAttributeValue<EntityReference>("keyvaultreferenceid");
        set { SetAttributeValue("keyvaultreferenceid", value); }
    }

    [AttributeLogicalName("keyvaultreferenceid_name")]
    public string KeyVaultReferenceIdName
    {
        get => GetAttributeValue<string>("keyvaultreferenceid_name");
        set { SetAttributeValue("keyvaultreferenceid_name", value); }
    }

    [AttributeLogicalName("messagecharset")]
    public OptionSetValue MessageCharset
    {
        get => GetAttributeValue<OptionSetValue>("messagecharset");
        set { SetAttributeValue("messagecharset", value); }
    }

    [AttributeLogicalName("messageformat")]
    public OptionSetValue MessageFormat
    {
        get => GetAttributeValue<OptionSetValue>("messageformat");
        set { SetAttributeValue("messageformat", value); }
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

    [AttributeLogicalName("namespaceaddress")]
    public string NamespaceAddress
    {
        get => GetAttributeValue<string>("namespaceaddress");
        set { SetAttributeValue("namespaceaddress", value); }
    }

    [AttributeLogicalName("namespaceformat")]
    public OptionSetValue NamespaceFormat
    {
        get => GetAttributeValue<OptionSetValue>("namespaceformat");
        set { SetAttributeValue("namespaceformat", value); }
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

    [AttributeLogicalName("path")]
    public string Path
    {
        get => GetAttributeValue<string>("path");
        set { SetAttributeValue("path", value); }
    }

    [AttributeLogicalName("runtimeintegrationproperties")]
    public string RuntimeIntegrationProperties
    {
        get => GetAttributeValue<string>("runtimeintegrationproperties");
        set { SetAttributeValue("runtimeintegrationproperties", value); }
    }

    [AttributeLogicalName("saskey")]
    public string SASKey
    {
        get => GetAttributeValue<string>("saskey");
        set { SetAttributeValue("saskey", value); }
    }

    [AttributeLogicalName("saskeyname")]
    public string SASKeyName
    {
        get => GetAttributeValue<string>("saskeyname");
        set { SetAttributeValue("saskeyname", value); }
    }

    [AttributeLogicalName("sastoken")]
    public string SASToken
    {
        get => GetAttributeValue<string>("sastoken");
        set { SetAttributeValue("sastoken", value); }
    }

    [AttributeLogicalName("schematype")]
    public OptionSetValue SchemaType
    {
        get => GetAttributeValue<OptionSetValue>("schematype");
        set { SetAttributeValue("schematype", value); }
    }

    [AttributeLogicalName("serviceendpointid")]
    public Guid? ServiceEndpointId
    {
        get => GetAttributeValue<Guid?>("serviceendpointid");
        set
        {
            SetAttributeValue("serviceendpointid", value);
            base.Id = value ?? Guid.Empty;
        }
    }

    [AttributeLogicalName("serviceendpointidunique")]
    public Guid? ServiceEndpointIdUnique
    {
        get => GetAttributeValue<Guid?>("serviceendpointidunique");
        set { SetAttributeValue("serviceendpointidunique", value); }
    }

    [AttributeLogicalName("solutionid")]
    public Guid? SolutionId
    {
        get => GetAttributeValue<Guid?>("solutionid");
        set { SetAttributeValue("solutionid", value); }
    }

    [AttributeLogicalName("solutionnamespace")]
    public string SolutionNamespace
    {
        get => GetAttributeValue<string>("solutionnamespace");
        set { SetAttributeValue("solutionnamespace", value); }
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

    [AttributeLogicalName("usekeyvaultconfiguration")]
    public bool? UseKeyVaultConfiguration
    {
        get => GetAttributeValue<bool?>("usekeyvaultconfiguration");
        set { SetAttributeValue("usekeyvaultconfiguration", value); }
    }

    [AttributeLogicalName("userclaim")]
    public OptionSetValue UserClaim
    {
        get => GetAttributeValue<OptionSetValue>("userclaim");
        set { SetAttributeValue("userclaim", value); }
    }
}
