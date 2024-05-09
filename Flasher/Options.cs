using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Flasher {

    public class Options
    {
        [Option('p', "port", HelpText = "Serial port to use")]
        public string Port { get; }

        [Option('d', "dev", HelpText = "DEVELOPER MODE: Printer model and version to be used")]
        public string PrinterModelVersion { get; }

        [Option('i', "input", HelpText = "Input firmware archive")]
        public string InputFileName { get; }

        [Option('m', "main", HelpText = "Run the Main Board flashing")]
        public bool FlashMain { get; }

        [Option('l', "lcd", HelpText = "Run the LCD flashing (Main Board must have been flashed)")]
        public bool FlashLCD { get; }

        [Option('c', "config", HelpText = "Upload specific .gc config. When used with --input overrides the config from archive")]
        public string PrinterConfig { get; }

        [Option("waitlcd", Default = "Status:I", HelpText = "Wait for specific line before LCD flashing")]
        public string WaitLCD { get; }

        [Option("waitcfg", Default = "Status:I", HelpText = "Wait for specific line before config flashing")]
        public string WaitConfig { get; }

        [Option('v', "verbose", HelpText = "Dump all the messages received via COM port")]
        public bool Verbose { get; }

        [Usage(ApplicationAlias = "flasher")]
        public static IEnumerable<Example> Examples =>
            new List<Example> {
                new ("Flash the printer all-at-once, Main, then LCD then config. May be slow", new Options("COM12", "", "MKA_1.2.6_ComposerA3_v1.0.x_24-03-2022_2127.fw2", true, true, "", null, null, false)),
                new ("Flash the printer all-at-once, Main and config, no LCD", new Options("COM12", "", "MKA_1.2.6_ComposerA3_v1.0.x_24-03-2022_2127.fw2", true, false, "", null, null, false)),
                new ("Flash the the .gc config only, developer mode, verbose COM", new Options("COM12", "ComposerA3 v1.0.3", "", false, false, "Composer_A3-1.0.3.gc", null, null, true)),
            };

        public Options(string port, string printerModelVersion, string inputFileName, bool flashMain, bool flashLCD, string printerConfig, string waitLCD, string waitConfig, bool verbose)
        {
            Port = port;
            PrinterModelVersion = printerModelVersion;
            InputFileName = inputFileName;
            FlashMain = flashMain;
            FlashLCD = flashLCD;
            PrinterConfig = printerConfig;
            WaitLCD = waitLCD;
            WaitConfig = waitConfig;
            Verbose = verbose;
        }
    }
}
