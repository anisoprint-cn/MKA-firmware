using CommandLine;
using System;
using System.Reflection;
using System.Runtime;
using Environment = System.Environment;

namespace FlasherCLI
{
    public class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Flasher.Options>(args)
                .WithParsed(Process)
                .WithNotParsed(_ => Environment.Exit(1));
        }

        static void Process(Flasher.Options options)
        {
            var flasher = new Flasher.Flasher(options);
            var result = flasher.Flash();

            Environment.Exit(result.code == Flasher.FlasherCode.SUCCESS ? 0 : 2);
        }
    }
}
