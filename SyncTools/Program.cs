using System.Diagnostics;
using System.Threading.Tasks;

namespace SyncTools
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            if (Debugger.IsAttached && args.Length == 0)
            {
                args = new string[]
                {
                        "--directory", @"c:\temp\sysinternals",
                        "--url", "https://live.sysinternals.com/",
                        "--verbose"
                };
            }

            await new SyncTools(args).Run();
        }
    }
}
