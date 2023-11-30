using Ardalis.GuardClauses;
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.New;

public class RequestBuilder<T>
    where T : OrganizationRequest
{
    private readonly T _request;

    public RequestBuilder(T request)
    {
        _request = Guard.Against.Null(request);
    }

    public T Build()
    {
        return _request;
    }

    /// <summary>
    ///     Known as 'tag'.
    ///     https://github.com/MicrosoftDocs/powerapps-docs/blob/main/powerapps-docs/developer/data-platform/org-service/use-messages.md#add-a-shared-variable-from-the-organization-service
    /// </summary>
    public RequestBuilder<T> WithSharedVariableName(string tagValue)
    {
        _request[RequestHeaders.TagName] = tagValue;

        return this;
    }

    /// <summary>
    ///     https://github.com/MicrosoftDocs/powerapps-docs/blob/main/powerapps-docs/developer/data-platform/org-service/use-messages.md#passing-optional-parameters-with-a-request
    /// </summary>
    public RequestBuilder<T> WithPartitionId(string partitionId)
    {
        _request[RequestHeaders.PartitionIdName] = partitionId;

        return this;
    }

    /// <summary>
    ///     <see cref="ConcurrencyBehavior" />
    ///     Specifies the type of optimistic concurrency behavior that should be performed by the Web service when processing
    ///     this request.
    ///     https://learn.microsoft.com/en-us/dotnet/api/microsoft.xrm.sdk.concurrencybehavior?view=dataverse-sdk-latest
    /// </summary>
    public RequestBuilder<T> WithConcurrencyBehavior(ConcurrencyBehavior concurrencyBehavior)
    {
        _request[RequestHeaders.ConcurrencyBehaviorName] = concurrencyBehavior;

        return this;
    }

    /// <summary>
    ///     A Boolean used to disable duplicate detection on a create or update operation.
    ///     https://github.com/MicrosoftDocs/powerapps-docs/blob/main/powerapps-docs/developer/data-platform/org-service/detect-duplicate-data.md#use-suppressduplicatedetection-parameter-to-throw-errors-when-you-create-or-update-row
    /// </summary>
    public RequestBuilder<T> WithSuppressDuplicateDetection(bool suppressDuplicateDetection)
    {
        _request[RequestHeaders.SuppressDuplicateDetectionName] = suppressDuplicateDetection;

        return this;
    }

    /// <summary>
    ///     Bypasses custom plug-ins when included in a request sent by someone with the prvBypassCustomPlugins privilege.
    ///     !The user sending the requests must have the prvBypassCustomPlugins privilege. By default, only users with the
    ///     system administrator security role have this privilege.!
    ///     https://github.com/MicrosoftDocs/powerapps-docs/blob/main/powerapps-docs/developer/data-platform/bypass-custom-business-logic.md
    /// </summary>
    public RequestBuilder<T> WithSkipPluginExecution(bool skipPluginExecution)
    {
        _request[RequestHeaders.BypassCustomPluginExecutionName] = skipPluginExecution;

        return this;
    }

    internal static class RequestHeaders
    {
        /// <summary>
        ///     This key used to indicate if the custom plug-ins need to be bypassed during the execution of the request.
        /// </summary>
        public const string BypassCustomPluginExecutionName = "BypassCustomPluginExecution";

        /// <summary>
        ///     used to identify concurrency behavior property in an organization request.
        /// </summary>
        public const string ConcurrencyBehaviorName = "ConcurrencyBehavior";

        public const string PartitionIdName = "PartitionId";

        /// <summary>
        ///     key used to apply the operation to a given solution.
        ///     See:
        ///     https://docs.microsoft.com/powerapps/developer/common-data-service/org-service/use-messages#passing-optional-parameters-with-a-request
        /// </summary>
        public const string SolutionUniqueNameName = "SolutionUniqueName";

        /// <summary>
        ///     used to apply duplicate detection behavior to a given request.
        ///     See:
        ///     https://docs.microsoft.com/powerapps/developer/common-data-service/org-service/use-messages#passing-optional-parameters-with-a-request
        /// </summary>
        public const string SuppressDuplicateDetectionName = "SuppressDuplicateDetection";

        /// <summary>
        ///     used to pass data though Dataverse to a plug-in or downstream system on a request.
        ///     See:
        ///     https://docs.microsoft.com/en-us/powerapps/developer/common-data-service/org-service/use-messages#add-a-shared-variable-from-the-organization-service
        /// </summary>
        public const string TagName = "tag";
    }
}
