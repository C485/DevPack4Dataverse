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
[EntityLogicalName("sdkmessageprocessingstep")]
internal partial class SdkMessageProcessingStep : Entity
{
    public const string EntityLogicalName = "sdkmessageprocessingstep";

    public const int EntityTypeCode = 4608;

    public SdkMessageProcessingStep() : base(EntityLogicalName) { }

    [AttributeLogicalName("asyncautodelete")]
    public bool? AsyncAutoDelete
    {
        get => GetAttributeValue<bool?>("asyncautodelete");
        set { SetAttributeValue("asyncautodelete", value); }
    }

    [AttributeLogicalName("canusereadonlyconnection")]
    public bool? CanUseReadOnlyConnection
    {
        get => GetAttributeValue<bool?>("canusereadonlyconnection");
        set { SetAttributeValue("canusereadonlyconnection", value); }
    }

    [AttributeLogicalName("category")]
    public string Category
    {
        get => GetAttributeValue<string>("category");
        set { SetAttributeValue("category", value); }
    }

    [AttributeLogicalName("componentstate")]
    public OptionSetValue ComponentState
    {
        get => GetAttributeValue<OptionSetValue>("componentstate");
        set { SetAttributeValue("componentstate", value); }
    }

    [AttributeLogicalName("configuration")]
    public string Configuration
    {
        get => GetAttributeValue<string>("configuration");
        set { SetAttributeValue("configuration", value); }
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

    [AttributeLogicalName("eventexpander")]
    public string EventExpander
    {
        get => GetAttributeValue<string>("eventexpander");
        set { SetAttributeValue("eventexpander", value); }
    }

    [AttributeLogicalName("eventhandler")]
    public EntityReference EventHandler
    {
        get => GetAttributeValue<EntityReference>("eventhandler");
        set { SetAttributeValue("eventhandler", value); }
    }

    [AttributeLogicalName("eventhandler_name")]
    public string EventHandlerName
    {
        get => GetAttributeValue<string>("eventhandler_name");
        set { SetAttributeValue("eventhandler_name", value); }
    }

    [AttributeLogicalName("filteringattributes")]
    public string FilteringAttributes
    {
        get => GetAttributeValue<string>("filteringattributes");
        set { SetAttributeValue("filteringattributes", value); }
    }

    [AttributeLogicalName("sdkmessageprocessingstepid")]
    public override Guid Id
    {
        get => base.Id;
        set => SdkMessageProcessingStepId = value;
    }

    [AttributeLogicalName("impersonatinguserid")]
    public EntityReference ImpersonatingUserId
    {
        get => GetAttributeValue<EntityReference>("impersonatinguserid");
        set { SetAttributeValue("impersonatinguserid", value); }
    }

    [AttributeLogicalName("impersonatinguserid_name")]
    public string ImpersonatingUserIdName
    {
        get => GetAttributeValue<string>("impersonatinguserid_name");
        set { SetAttributeValue("impersonatinguserid_name", value); }
    }

    [AttributeLogicalName("introducedversion")]
    public string IntroducedVersion
    {
        get => GetAttributeValue<string>("introducedversion");
        set { SetAttributeValue("introducedversion", value); }
    }

    [AttributeLogicalName("invocationsource")]
    [System.Obsolete]
    public OptionSetValue InvocationSource
    {
        get => GetAttributeValue<OptionSetValue>("invocationsource");
        set { SetAttributeValue("invocationsource", value); }
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

    [AttributeLogicalName("mode")]
    public OptionSetValue Mode
    {
        get => GetAttributeValue<OptionSetValue>("mode");
        set { SetAttributeValue("mode", value); }
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

    [AttributeLogicalName("plugintypeid")]
    [System.Obsolete]
    public EntityReference PluginTypeId
    {
        get => GetAttributeValue<EntityReference>("plugintypeid");
        set { SetAttributeValue("plugintypeid", value); }
    }

    [AttributeLogicalName("plugintypeid_name")]
    [System.Obsolete]
    public string PluginTypeIdName
    {
        get => GetAttributeValue<string>("plugintypeid_name");
        set { SetAttributeValue("plugintypeid_name", value); }
    }

    [AttributeLogicalName("powerfxruleid")]
    public EntityReference PowerfxRuleId
    {
        get => GetAttributeValue<EntityReference>("powerfxruleid");
        set { SetAttributeValue("powerfxruleid", value); }
    }

    [AttributeLogicalName("powerfxruleid_name")]
    public string PowerfxRuleIdName
    {
        get => GetAttributeValue<string>("powerfxruleid_name");
        set { SetAttributeValue("powerfxruleid_name", value); }
    }

    [AttributeLogicalName("rank")]
    public int? Rank
    {
        get => GetAttributeValue<int?>("rank");
        set { SetAttributeValue("rank", value); }
    }

    [AttributeLogicalName("runtimeintegrationproperties")]
    public string RuntimeIntegrationProperties
    {
        get => GetAttributeValue<string>("runtimeintegrationproperties");
        set { SetAttributeValue("runtimeintegrationproperties", value); }
    }

    [AttributeLogicalName("sdkmessagefilterid")]
    public EntityReference SdkMessageFilterId
    {
        get => GetAttributeValue<EntityReference>("sdkmessagefilterid");
        set { SetAttributeValue("sdkmessagefilterid", value); }
    }

    [AttributeLogicalName("sdkmessagefilterid_name")]
    public string SdkMessageFilterIdName
    {
        get => GetAttributeValue<string>("sdkmessagefilterid_name");
        set { SetAttributeValue("sdkmessagefilterid_name", value); }
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

    [AttributeLogicalName("sdkmessageprocessingstepid")]
    public Guid? SdkMessageProcessingStepId
    {
        get => GetAttributeValue<Guid?>("sdkmessageprocessingstepid");
        set
        {
            SetAttributeValue("sdkmessageprocessingstepid", value);
            base.Id = value ?? Guid.Empty;
        }
    }

    [AttributeLogicalName("sdkmessageprocessingstepidunique")]
    public Guid? SdkMessageProcessingStepIdUnique
    {
        get => GetAttributeValue<Guid?>("sdkmessageprocessingstepidunique");
        set { SetAttributeValue("sdkmessageprocessingstepidunique", value); }
    }

    [AttributeLogicalName("sdkmessageprocessingstepsecureconfigid")]
    public EntityReference SdkMessageProcessingStepSecureConfigId
    {
        get => GetAttributeValue<EntityReference>("sdkmessageprocessingstepsecureconfigid");
        set { SetAttributeValue("sdkmessageprocessingstepsecureconfigid", value); }
    }

    [AttributeLogicalName("sdkmessageprocessingstepsecureconfigid_name")]
    public string SdkMessageProcessingStepSecureConfigIdName
    {
        get => GetAttributeValue<string>("sdkmessageprocessingstepsecureconfigid_name");
        set { SetAttributeValue("sdkmessageprocessingstepsecureconfigid_name", value); }
    }

    [AttributeLogicalName("solutionid")]
    public Guid? SolutionId
    {
        get => GetAttributeValue<Guid?>("solutionid");
        set { SetAttributeValue("solutionid", value); }
    }

    [AttributeLogicalName("stage")]
    public OptionSetValue Stage
    {
        get => GetAttributeValue<OptionSetValue>("stage");
        set { SetAttributeValue("stage", value); }
    }

    [AttributeLogicalName("statecode")]
    public SdkMessageProcessingStepState? StateCode
    {
        get
        {
            OptionSetValue optionSet = GetAttributeValue<OptionSetValue>("statecode");
            if (optionSet != null)
            {
                return (SdkMessageProcessingStepState)(
                    Enum.ToObject(typeof(SdkMessageProcessingStepState), optionSet.Value)
                );
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
    public OptionSetValue StatusCode
    {
        get => GetAttributeValue<OptionSetValue>("statuscode");
        set { SetAttributeValue("statuscode", value); }
    }

    [AttributeLogicalName("supporteddeployment")]
    public OptionSetValue SupportedDeployment
    {
        get => GetAttributeValue<OptionSetValue>("supporteddeployment");
        set { SetAttributeValue("supporteddeployment", value); }
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
