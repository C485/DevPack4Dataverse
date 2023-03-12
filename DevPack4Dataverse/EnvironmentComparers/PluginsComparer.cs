using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using DevPack4Dataverse.Entities;

namespace DevPack4Dataverse.EnvironmentComparers
{
    public enum DifferenceType
    {
        ExistsOnlyInTarget,
        ExistsOnlyInSource,
        Different
    }

    public class DifferenceBase
    {
        public DifferenceType DifferenceType { get; set; }
        public int ComponentType { get; set; }
    }

    public class PluginAssemblyDifference : DifferenceBase { }

    /*
        90 	Plugin Type 	plugintype 	 Plugin type 	 RetrieveRequest (Target = plugintype)
        91 	Plugin Assembly 	pluginassembly 	 Plugin assembly 	 RetrieveRequest (Target = pluginassembly)
        92 	SDK Message Processing Step 	sdkmessageprocessingstep 	 Plugin step 	 RetrieveRequest (Target = sdkmessageprocessingstep)
        93 	SDK Message Processing Step Image 	sdkmessageprocessingstepimage 	 Plugin step image 	 RetrieveRequest (Target = sdkmessageprocessingstepimage)
     
     */
    internal sealed class PluginsComparer
    {
        public PluginsComparer(DataverseDevPack sourceDataverseDevPack, DataverseDevPack targetDataverseDevPack)
        {
            _sourceDataverseDevPack = Guard.Against.Null(sourceDataverseDevPack);
            _targetDataverseDevPack = Guard.Against.Null(targetDataverseDevPack);
        }

        private readonly DataverseDevPack _sourceDataverseDevPack;
        private readonly DataverseDevPack _targetDataverseDevPack;
        private PluginAssembly[] sourcePlugins = Array.Empty<PluginAssembly>();
        private PluginAssembly[] targetPlugins = Array.Empty<PluginAssembly>();
        private SdkMessageProcessingStep[] sourcePluginsSteps = Array.Empty<SdkMessageProcessingStep>();
        private SdkMessageProcessingStep[] targetPluginsSteps = Array.Empty<SdkMessageProcessingStep>();
        private SdkMessageProcessingStepSecureConfig[] sourcePluginsStepsSecureConfig =
            Array.Empty<SdkMessageProcessingStepSecureConfig>();
        private SdkMessageProcessingStepSecureConfig[] targetPluginsStepsSecureConfig =
            Array.Empty<SdkMessageProcessingStepSecureConfig>();

        private SdkMessageProcessingStepImage[] sourcePluginsStepsImages = Array.Empty<SdkMessageProcessingStepImage>();
        private SdkMessageProcessingStepImage[] targetPluginsStepsImages = Array.Empty<SdkMessageProcessingStepImage>();

        public async IAsyncEnumerable<DifferenceBase> Execute()
        {
            await DownloadPlugins();
            await DownloadPluginsSteps();
            await DownloadPluginsStepsSecureConfig();
            await DownloadPluginsStepsImages();
            yield break;
        }

        private async Task DownloadPluginsStepsImages()
        {
            using ConnectionLease sourceConnection = await _sourceDataverseDevPack.SdkProxy.GetConnectionAsync();
            using ConnectionLease targetConnection = await _targetDataverseDevPack.SdkProxy.GetConnectionAsync();
            Expression<Func<SdkMessageProcessingStepImage, SdkMessageProcessingStepImage>> selector = p =>
                new SdkMessageProcessingStepImage
                {
                    Name = p.Name,
                    SdkMessageProcessingStepId = p.SdkMessageProcessingStepId,
                    SdkMessageProcessingStepImageId = p.SdkMessageProcessingStepImageId,
                    Attributes1 = p.Attributes1,
                    Description = p.Description,
                    EntityAlias = p.EntityAlias,
                    EntityState = p.EntityState,
                    ImageType = p.ImageType,
                    IsManaged = p.IsManaged,
                    MessagePropertyName = p.MessagePropertyName,
                    IsCustomizable = p.IsCustomizable,
                    CustomizationLevel = p.CustomizationLevel,
                    OrganizationId = p.OrganizationId
                };
            sourcePluginsStepsImages = sourceConnection
                .GetOrganizationServiceContext<XrmServiceContext>()
                .SdkMessageProcessingStepImageSet.Select(selector)
                .ToArray();
            targetPluginsStepsImages = targetConnection
                .GetOrganizationServiceContext<XrmServiceContext>()
                .SdkMessageProcessingStepImageSet.Select(selector)
                .ToArray();
        }

        private async Task DownloadPluginsStepsSecureConfig()
        {
            using ConnectionLease sourceConnection = await _sourceDataverseDevPack.SdkProxy.GetConnectionAsync();
            using ConnectionLease targetConnection = await _targetDataverseDevPack.SdkProxy.GetConnectionAsync();
            Expression<Func<SdkMessageProcessingStepSecureConfig, SdkMessageProcessingStepSecureConfig>> selector = p =>
                new SdkMessageProcessingStepSecureConfig
                {
                    SecureConfig = p.SecureConfig,
                    SdkMessageProcessingStepSecureConfigId = p.SdkMessageProcessingStepSecureConfigId,
                    CustomizationLevel = p.CustomizationLevel,
                    OrganizationId = p.OrganizationId
                };
            sourcePluginsStepsSecureConfig = sourceConnection
                .GetOrganizationServiceContext<XrmServiceContext>()
                .SdkMessageProcessingStepSecureConfigSet.Select(selector)
                .ToArray();
            targetPluginsStepsSecureConfig = targetConnection
                .GetOrganizationServiceContext<XrmServiceContext>()
                .SdkMessageProcessingStepSecureConfigSet.Select(selector)
                .ToArray();
        }

        private async Task DownloadPluginsSteps()
        {
            using ConnectionLease sourceConnection = await _sourceDataverseDevPack.SdkProxy.GetConnectionAsync();
            using ConnectionLease targetConnection = await _targetDataverseDevPack.SdkProxy.GetConnectionAsync();
            Expression<Func<SdkMessageProcessingStep, SdkMessageProcessingStep>> selector = p =>
                new SdkMessageProcessingStep
                {
                    Name = p.Name,
                    ComponentState = p.ComponentState,
                    CustomizationLevel = p.CustomizationLevel,
                    IsManaged = p.IsManaged,
                    IsHidden = p.IsHidden,
                    VersionNumber = p.VersionNumber,
                    IntroducedVersion = p.IntroducedVersion,
                };
            sourcePluginsSteps = sourceConnection
                .GetOrganizationServiceContext<XrmServiceContext>()
                .SdkMessageProcessingStepSet.Select(selector)
                .ToArray();
            targetPluginsSteps = targetConnection
                .GetOrganizationServiceContext<XrmServiceContext>()
                .SdkMessageProcessingStepSet.Select(selector)
                .ToArray();
        }

        private async Task DownloadPlugins()
        {
            using ConnectionLease sourceConnection = await _sourceDataverseDevPack.SdkProxy.GetConnectionAsync();
            using ConnectionLease targetConnection = await _targetDataverseDevPack.SdkProxy.GetConnectionAsync();
            Expression<Func<PluginAssembly, PluginAssembly>> selector = p =>
                new PluginAssembly
                {
                    Name = p.Name,
                    Version = p.Version,
                    IsolationMode = p.IsolationMode,
                    SourceHash = p.SourceHash,
                    ComponentState = p.ComponentState,
                    Major = p.Major,
                    Minor = p.Minor,
                    UserName = p.UserName,
                    CustomizationLevel = p.CustomizationLevel,
                    IsManaged = p.IsManaged,
                    IsHidden = p.IsHidden,
                    IsPasswordSet = p.IsPasswordSet,
                    Password = p.Password,
                    Path = p.Path,
                    PluginAssemblyId = p.PluginAssemblyId,
                    SourceType = p.SourceType,
                    VersionNumber = p.VersionNumber,
                    IntroducedVersion = p.IntroducedVersion
                };
            sourcePlugins = sourceConnection
                .GetOrganizationServiceContext<XrmServiceContext>()
                .PluginAssemblySet.Select(selector)
                .ToArray();
            targetPlugins = targetConnection
                .GetOrganizationServiceContext<XrmServiceContext>()
                .PluginAssemblySet.Select(selector)
                .ToArray();
        }
    }
}
