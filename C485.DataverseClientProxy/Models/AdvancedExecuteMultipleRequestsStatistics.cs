using System.Diagnostics;

namespace C485.DataverseClientProxy.Models;

public class AdvancedExecuteMultipleRequestsStatistics
{
	public int RecordsProcessed { get; set; }

	public int RecordsRequested { get; set; }

	public Stopwatch Stopwatch { get; set; }

	public int ThreadsUsed { get; set; }

	public bool Cancelled { get; set; }
}