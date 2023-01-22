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

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

namespace DevPack4Dataverse.Entities;

internal class XrmServiceContext : OrganizationServiceContext
{
    public XrmServiceContext(IOrganizationService service) : base(service) { }

    public IQueryable<PluginAssembly> PluginAssemblySet => CreateQuery<PluginAssembly>();
    public IQueryable<PluginPackage> PluginPackageSet => CreateQuery<PluginPackage>();
    public IQueryable<PluginType> PluginTypeSet => CreateQuery<PluginType>();
    public IQueryable<SdkMessageFilter> SdkMessageFilterSet => CreateQuery<SdkMessageFilter>();

    public IQueryable<SdkMessageProcessingStepImage> SdkMessageProcessingStepImageSet =>
        CreateQuery<SdkMessageProcessingStepImage>();

    public IQueryable<SdkMessageProcessingStepSecureConfig> SdkMessageProcessingStepSecureConfigSet =>
        CreateQuery<SdkMessageProcessingStepSecureConfig>();

    public IQueryable<SdkMessageProcessingStep> SdkMessageProcessingStepSet => CreateQuery<SdkMessageProcessingStep>();
    public IQueryable<SdkMessage> SdkMessageSet => CreateQuery<SdkMessage>();
    public IQueryable<ServiceEndpoint> ServiceEndpointSet => CreateQuery<ServiceEndpoint>();
}
