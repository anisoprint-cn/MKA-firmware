using CommandLine;
using System;
using System.Reflection;
using System.Runtime;
using Environment = System.Environment;

namespace Flasher
{
    public class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(Process)
                .WithNotParsed(_ => Environment.Exit(1));
        }

        static void Process(Options options)
        {
            var flasher = new Flasher(options);
            var result = flasher.Flash();

            Environment.Exit(result.code == FlasherCode.SUCCESS ? 0 : 2);
        }
    }
}
