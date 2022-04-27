using C485.DataverseClientProxy.Playground.Examples;
using System.Threading.Tasks;

namespace C485.DataverseClientProxy.Playground
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            await new AdvancedMultipleRequest().Execute();
            await new AdvancedMultipleRequestDelete().Execute();
        }
    }
}