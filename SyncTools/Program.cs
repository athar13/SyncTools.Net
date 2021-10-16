using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SyncTools
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            if (Debugger.IsAttached)
            {
                if (args.Length == 0)
                {
                    args = new string[]
                    {
                        "--directory", @"c:\temp\sysinternals",
                        "--url", "https://live.sysinternals.com/",
                        "--verbose"
                    };
                }
            }

            await new SyncTools(args).Run();
        }
    }
}
