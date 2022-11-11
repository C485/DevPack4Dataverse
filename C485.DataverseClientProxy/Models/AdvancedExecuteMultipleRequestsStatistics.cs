using System.Diagnostics;

namespace C485.DataverseClientProxy.Models;

public class AdvancedExecuteMultipleRequestsStatistics
{
    public bool Cancelled { get; set; }
    public int RecordsProcessed { get; set; }

    public int RecordsRequested { get; set; }

    public Stopwatch Stopwatch { get; set; }

    public int ThreadsUsed { get; set; }
}