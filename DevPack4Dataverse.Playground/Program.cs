using C485.DataverseClientProxy.Playground.Examples;
using Serilog;
using Serilog.Extensions.Logging;
using System.Threading.Tasks;

namespace DevPack4Dataverse.Playground
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Console().CreateLogger();
            var microsoftLogger = new SerilogLoggerFactory(Log.Logger)
                .CreateLogger("");
            await new AdvancedMultipleRequest(microsoftLogger).Execute();
            await new AdvancedMultipleRequestDelete(microsoftLogger).Execute();
            Log.CloseAndFlush();
        }
    }
}