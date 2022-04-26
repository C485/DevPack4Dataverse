using System.Diagnostics;

namespace C485.DataverseClientProxy.Models
{
    public class ChunksStatistics
    {
        public int RecordsProcessed { get; set; }
        public Stopwatch Stopwatch { get; set; }
        public int ThreadsUsed { get; set; }
    }
}