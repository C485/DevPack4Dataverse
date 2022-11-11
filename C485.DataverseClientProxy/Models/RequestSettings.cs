using Microsoft.Xrm.Sdk;

namespace C485.DataverseClientProxy.Models;

public class RequestImpersonateSettings
{
    public Guid? ImpersonateAsUserByAADId { get; set; }

    /// <summary>
    ///  Guid of SystemUser record id
    /// </summary>
    public Guid? ImpersonateAsUserByDataverseId { get; set; }
}

public sealed class RequestSettings : RequestImpersonateSettings
{
    /// <summary>
    /// <see cref="ConcurrencyBehavior"/>
    /// Specifies the type of optimistic concurrency behavior that should be performed by the Web service when processing this request.
    /// https://learn.microsoft.com/en-us/dotnet/api/microsoft.xrm.sdk.concurrencybehavior?view=dataverse-sdk-latest
    /// </summary>
    public ConcurrencyBehavior? ConcurrencyBehavior { get; set; }

    /// <summary>
    /// https://github.com/MicrosoftDocs/powerapps-docs/blob/main/powerapps-docs/developer/data-platform/org-service/use-messages.md#passing-optional-parameters-with-a-request
    /// </summary>
    public string PartitionId { private get; set; }

    /// <summary>
    /// Known as 'tag'.
    /// https://github.com/MicrosoftDocs/powerapps-docs/blob/main/powerapps-docs/developer/data-platform/org-service/use-messages.md#add-a-shared-variable-from-the-organization-service
    /// </summary>
    public string SharedVariable { private get; set; }

    /// <summary>
    /// Bypasses custom plug-ins when included in a request sent by someone with the prvBypassCustomPlugins privilege.
    /// !The user sending the requests must have the prvBypassCustomPlugins privilege. By default, only users with the system administrator security role have this privilege.!
    /// https://github.com/MicrosoftDocs/powerapps-docs/blob/main/powerapps-docs/developer/data-platform/bypass-custom-business-logic.md
    /// </summary>
	public bool SkipPluginExecution { private get; set; }

    /// <summary>
    /// A Boolean used to disable duplicate detection on a create or update operation.
    /// https://github.com/MicrosoftDocs/powerapps-docs/blob/main/powerapps-docs/developer/data-platform/org-service/detect-duplicate-data.md#use-suppressduplicatedetection-parameter-to-throw-errors-when-you-create-or-update-row
    /// </summary>
    public bool SuppressDuplicateDetection { private get; set; }

    public void AddToOrganizationRequest(OrganizationRequest organizationRequest)
    {
        if (!string.IsNullOrEmpty(SharedVariable))
        {
            organizationRequest[RequestHeaders.TAG] = SharedVariable;
        }
        if (!string.IsNullOrEmpty(PartitionId))
        {
            organizationRequest[RequestHeaders.PartitionId] = PartitionId;
        }
        if (SuppressDuplicateDetection)
        {
            organizationRequest[RequestHeaders.SUPPRESSDUPLICATEDETECTION] = true;
        }
        if (SkipPluginExecution)
        {
            organizationRequest[RequestHeaders.BYPASSCUSTOMPLUGINEXECUTION] = true;
        }
        if (ConcurrencyBehavior.HasValue)
        {
            organizationRequest[RequestHeaders.CONCURRENCYBEHAVIOR] = ConcurrencyBehavior.Value;
        }
    }

    internal static class RequestHeaders
    {
        /// <summary>
        /// This key used to indicate if the custom plugins need to be bypassed during the execution of the request.
        /// </summary>
        public const string BYPASSCUSTOMPLUGINEXECUTION = "BypassCustomPluginExecution";

        /// <summary>
        /// used to identify concurrencybehavior property in an organization request.
        /// </summary>
        public const string CONCURRENCYBEHAVIOR = "ConcurrencyBehavior";

        public const string PartitionId = "PartitionId";

        /// <summary>
        /// key used to apply the operation to a given solution.
        /// See: https://docs.microsoft.com/powerapps/developer/common-data-service/org-service/use-messages#passing-optional-parameters-with-a-request
        /// </summary>
        public const string SOLUTIONUNIQUENAME = "SolutionUniqueName";

        /// <summary>
        /// used to apply duplicate detection behavior to a given request.
        /// See: https://docs.microsoft.com/powerapps/developer/common-data-service/org-service/use-messages#passing-optional-parameters-with-a-request
        /// </summary>
        public const string SUPPRESSDUPLICATEDETECTION = "SuppressDuplicateDetection";

        /// <summary>
        /// used to pass data though Dataverse to a plugin or downstream system on a request.
        /// See: https://docs.microsoft.com/en-us/powerapps/developer/common-data-service/org-service/use-messages#add-a-shared-variable-from-the-organization-service
        /// </summary>
        public const string TAG = "tag";
    }
}