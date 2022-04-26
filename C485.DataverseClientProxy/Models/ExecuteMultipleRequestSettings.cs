using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System;

namespace C485.DataverseClientProxy.Models
{
    public class ExecuteMultipleRequestSettings
    {
        /// <summary>
        /// <para>Callback used for reporting error.</para>
        /// <para>
        /// Called each time when error occours, please have in mind that thread may warry bettwen errors.
        /// </para>
        /// </summary>
        public Action<OrganizationRequest, string> ErrorReport { get; set; }

        /// <summary>
        /// <para>Represents a number of record packs which size is definied by <see cref="RequestSize"/>.</para>
        /// <para>This number should be equal or less to amount of connections in <see cref="DataverseClientProxy"/>.</para>
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = -1;

        /// <summary>
        /// <para>Callback used for reporting progress.</para>
        /// <para>It's executed every <see cref="ReportProgressInterval"/> from separate thread.</para>
        /// <para>Any access to objects from other threads needs to be atomic/locked.</para>
        /// </summary>
        public Action<int, int> ReportProgress { get; set; }

        /// <summary>
        /// <para>Sets sleep interval for thread that is used for reporting progress.</para>
        /// <para>See <see cref="ReportProgress"/> callback.</para>
        /// </summary>
        public TimeSpan ReportProgressInterval { get; set; }

        /// <summary>
        /// <para>Represents a number of records that will be send to Dataverse in one <see cref="ExecuteMultipleRequest"/></para>
        /// <para>By default this number is set to 60, which in benchmarks gave best performance.</para>
        /// <para>See more informaction at project site.</para>
        /// </summary>
        public int RequestSize { get; set; } = 60;
    }
}