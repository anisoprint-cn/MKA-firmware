using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using Timer = System.Timers.Timer;
using Serilog;

namespace Flasher
{
    public enum FlasherCommands
    {
        GET_PRINTER_VERSION,
        GET_PRINTER_FIRMWARE_VERSION,
        GET_PRINTER_MACHINE_NAME,
        PRINTER_FIRWARE_UPDATE_STARTED,
        DISPLAY_FIRMWARE_UPDATE_STARTED,
        DISPLAY_FIRMWARING,
        SYSTEM_CONFIG_VERSION,
        SYSTEM_CONFIG_CRC,
        EEPROM_WRITING,
        SAVE_PRINTER_VERSION_TO_EEPROM
    }

    public enum FlasherCode
    {
        NONE,
        SUCCESS,
        FLASHER_OPTIONS_INCORRECT,
        PRINTER_NOT_READY,
        PRINTER_MODEL_CANT_BE_RECEIVED,
        FIRMWARE_NOT_VERIFIED,
        FIRMWARE_FILE_INCORRECT,
        PRINTER_MODEL_INCORRECT,
        DISPLAY_FILE_INCORRECT,
        DISPLAY_NOT_READY,
        DISPLAY_FIRMWARING_ERROR,
        UNKNOWN_ERROR,
        UPLOADING_ERROR,
        CONFIG_WRITTING_ERROR,
        EEPROM_WRITTING_ERROR,
    }

    public enum FlasherStatus
    {
        PRINTER_READY,
        PRINTER_MODEL_VERIFIED,
        FIRMWARE_VERIFIED,
        DISPLAY_READY,
        EEPROM_UPDATED,
        DISPLAY_UPDATED
    }

    public class Flasher
    {
        private const string _firmwareFileName = "MKA.bin";
        private const string _displayFileName = "MKA.tft";
        private Options _options;

        private const int BUFFER_SIZE = 128;

        public static List<string> GetSerialPorts()
        {
            return SerialPort.GetPortNames().ToList();
        }

        private readonly Dictionary<FlasherCommands, (string command, string pattern)> _commandAndAnswerPatterns = new Dictionary<FlasherCommands, (string, string)>()
        {
            {FlasherCommands.GET_PRINTER_VERSION, ("M1006", "VER:")},
            {FlasherCommands.GET_PRINTER_FIRMWARE_VERSION, ("M1007", "FVWER:") },
            {FlasherCommands.GET_PRINTER_MACHINE_NAME, ("M1008", "MACHINE:") },
            {FlasherCommands.PRINTER_FIRWARE_UPDATE_STARTED, ("M1009", "") },
            {FlasherCommands.DISPLAY_FIRMWARE_UPDATE_STARTED, ("M35", "RESULT:") },
            {FlasherCommands.DISPLAY_FIRMWARING, ("", "RESULT:") },
            {FlasherCommands.SYSTEM_CONFIG_VERSION, ("M503 S V", "VER:") },
            {FlasherCommands.SYSTEM_CONFIG_CRC, ("M503 S R", "CRC:") },
            {FlasherCommands.EEPROM_WRITING, ("M500 S", "RESULT:") },
            {FlasherCommands.SAVE_PRINTER_VERSION_TO_EEPROM, ("M1004", "RESULT:") }
        };

        public event EventHandler<FlasherStatus> Updated;

        public Flasher(Options options)
        {
            _options = options;
        }

        public (FlasherCode code, string errorMessage) Flash()
        {
            bool devMode = !string.IsNullOrEmpty(_options.PrinterModelVersion);

            SerialPort sp = null;
            System.Diagnostics.Process process = null;
            string tempDirectory = null;
            var count = 0;
            var packetPos = 0;

            //создание серийного порта
            sp = new SerialPort();
            sp.PortName = _options.Port;
            sp.BaudRate = 115200;
            sp.Encoding = Encoding.ASCII;
            sp.DtrEnable = true;
            sp.RtsEnable = true;

            try
            {
                Log.Information($"Opening serial port {sp.PortName}");
                sp.Open();
            } catch (Exception e)
            {
                Log.Information($"Failed to open serial port {sp.PortName}: {e.Message}");
                //если есть ошибка, то возвращаем и ее текст
                return (FlasherCode.UNKNOWN_ERROR, e.Message);
            }

            try
            {
                //sp.WriteLine(_commandAndAnswerPatterns[FlasherCommands.PRINTER_FIRWARE_UPDATE_STARTED].command);

                (string value, List<string> allStrs, bool hasError) machineName = ("", new List<string>(), false);
                (string value, List<string> allStrs, bool hasError) machineVersion = ("", new List<string>(), false);
                (string value, List<string> allStrs, bool hasError) configVersionFromMachine = ("", new List<string>(), false);
                (string value, List<string> allStrs, bool hasError) configCRC = ("", new List<string>(), false);
                if (!devMode)
                {
                    Log.Information("Gathering printer info");
                    Thread.Sleep(1000);
                    sp.WriteLine(_commandAndAnswerPatterns[FlasherCommands.PRINTER_FIRWARE_UPDATE_STARTED].command);
                    Log.Information("Printer firmware update started");
                    Thread.Sleep(1000);
                    //~ sp.WriteLine("Sending PRINTER_FIRWARE_UPDATE_STARTED command");
                    Log.Information(nameof(FlasherCommands.PRINTER_FIRWARE_UPDATE_STARTED));

                    //ждем 60 секунд, пока принтер накатается
                    sp.DiscardInBuffer();
                    Log.Information("Waiting for 'wait' line");
                    var res = WaitFor(sp, "wait", 60);
                    if (!res.ok)
                    {
                        LoggingError("'wait' was not received", res.allStrs);
                        return (FlasherCode.PRINTER_NOT_READY, "");
                    }

                    Log.Information("Printer is ready");
                    UpdateStatus(FlasherStatus.PRINTER_READY);

                    Thread.Sleep(5000);

                    sp.DiscardInBuffer();
                    Log.Information("Getting machine version");
                    sp.WriteLine(_commandAndAnswerPatterns[FlasherCommands.GET_PRINTER_VERSION].command);

                    machineVersion = GetChars(sp, 60, _commandAndAnswerPatterns[FlasherCommands.GET_PRINTER_VERSION].pattern);
                    Log.Information($"Received version: {machineVersion.value}");

                    if (machineVersion.hasError)
                    {
                        LoggingError("Can't receive machine version", machineVersion.allStrs);
                        return (FlasherCode.PRINTER_MODEL_CANT_BE_RECEIVED, "");
                    }
                    if (machineVersion.value.Count(a => a.Equals('.')) == 1) machineVersion.value += ".0";

                    sp.DiscardInBuffer();
                    Log.Information("Getting machine name");
                    sp.WriteLine(_commandAndAnswerPatterns[FlasherCommands.GET_PRINTER_MACHINE_NAME].command);

                    machineName = GetChars(sp, 10, _commandAndAnswerPatterns[FlasherCommands.GET_PRINTER_MACHINE_NAME].pattern);
                    Log.Information($"Received name: {machineName.value}");

                    if (machineName.hasError)
                    {
                        LoggingError("Can't receive machine name", machineName.allStrs);
                        return (FlasherCode.PRINTER_MODEL_CANT_BE_RECEIVED, "");
                    }

                    sp.DiscardInBuffer();
                    Log.Information("Getting machine system config version");
                    sp.WriteLine(_commandAndAnswerPatterns[FlasherCommands.SYSTEM_CONFIG_VERSION].command);

                    configVersionFromMachine = GetChars(sp, 10, _commandAndAnswerPatterns[FlasherCommands.SYSTEM_CONFIG_VERSION].pattern);
                    Log.Information($"Received config version: {configVersionFromMachine.value}");

                    sp.DiscardInBuffer();
                    Log.Information("Getting machine system config CRC");
                    sp.WriteLine(_commandAndAnswerPatterns[FlasherCommands.SYSTEM_CONFIG_CRC].command);

                    configCRC = GetChars(sp, 10, _commandAndAnswerPatterns[FlasherCommands.SYSTEM_CONFIG_CRC].pattern);
                    Log.Information($"Received config CRC: {configCRC.value}");
                }
                else
                {
                    Log.Information("Using developer mode!");
                    Log.Information("Skipping printer readiness checks");
                    UpdateStatus(FlasherStatus.PRINTER_READY);
                    UpdateStatus(FlasherStatus.PRINTER_MODEL_VERIFIED);
                }

                string printerFileNameTemplate = _options.PrinterModelVersion;
                var readArchive = (ZipArchive z) => {
                    if (!devMode)
                    {
                        printerFileNameTemplate = machineName.value + "-" + machineVersion.value;
                        var hasFound = false;
                        foreach (var e in z.Entries)
                        {
                            var realFileName = Path.GetFileNameWithoutExtension(e.Name);
                            if (printerFileNameTemplate.Equals(realFileName))
                            {
                                hasFound = true;
                                break;
                            }
                        }

                        if (!hasFound)
                        {
                            LoggingError("Can't find printer model:", new List<string>(){ printerFileNameTemplate });
                            return (FlasherCode.PRINTER_MODEL_INCORRECT, "");
                        }
                        else
                        {
                            Log.Information("Printer model is verified, config is available");
                            UpdateStatus(FlasherStatus.PRINTER_MODEL_VERIFIED);
                        }
                    }
                    else
                    {
                        // TODO: Check if input has the corresponding config if no override is present
                        Log.Information("Skipping printer model verification");
                        UpdateStatus(FlasherStatus.PRINTER_MODEL_VERIFIED);
                    }

                    tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    Directory.CreateDirectory(tempDirectory);

                    if (_options.FlashMain) {
                        var boardFirmware = z.GetEntry(_firmwareFileName);
                        if (boardFirmware == null)
                        {
                            LoggingError("Can't find firmware file: ", new List<string>() { _firmwareFileName });
                            return (FlasherCode.FIRMWARE_FILE_INCORRECT, "");
                        }
                        var tempBoardName = Path.Combine(tempDirectory, _firmwareFileName);
                        boardFirmware.ExtractToFile(tempBoardName);

                        Log.Information("Starting MCU firmware update");
                        //команда того, что наичнается прошивка принтера
                        sp.WriteLine(_commandAndAnswerPatterns[FlasherCommands.PRINTER_FIRWARE_UPDATE_STARTED].command);

                        process = new System.Diagnostics.Process();
                        var startInfo = new System.Diagnostics.ProcessStartInfo { WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal };
                        var applicationPath = AppDomain.CurrentDomain.BaseDirectory;
                        var bossacPath = Path.Combine(applicationPath, "bossac.exe");
                        startInfo.FileName = bossacPath;
                        startInfo.Arguments = $" --port={_options.Port} -U false -e -w -b \"{tempBoardName}\" -R";
                        startInfo.RedirectStandardOutput = true;
                        startInfo.RedirectStandardError = true;
                        startInfo.UseShellExecute = false;
                        process.StartInfo = startInfo;

                        sp.Close();
                        sp.BaudRate = 1200;
                        sp.Open();
                        Thread.Sleep(1000);
                        sp.Close();
                        sp.BaudRate = 115200;

                        process.Start();

                        while (!process.HasExited)
                        {

                        }

                        var bossacLog = process.StandardOutput.ReadToEnd();
                        var bossacErrors = process.StandardError.ReadToEnd();
                        Log.Information($"Bossac log:\n{bossacLog}");

                        var exitCode = process.ExitCode;
                        process.Close();
                        switch (exitCode)
                        {
                            case 1:
                                LoggingError("Firmware upload failed", new List<string>(){ bossacErrors });
                                return (FlasherCode.UPLOADING_ERROR, "");
                            case 2:
                                LoggingError("Firmware verification failed", new List<string>() { bossacErrors });
                                return (FlasherCode.FIRMWARE_NOT_VERIFIED, "");
                            case 0:
                                Log.Information("Firmware successfully uploaded and verified");
                                UpdateStatus(FlasherStatus.FIRMWARE_VERIFIED);
                                break;
                        }

                        Thread.Sleep(2000);

                        Log.Information("Reconnecting with the machine");
                        sp.Open();

                        Thread.Sleep(1000);
                    }
                    if (_options.FlashLCD) {
                        //экранная часть

                        Log.Information("Sending LCD firmware update command");
                        sp.WriteLine(_commandAndAnswerPatterns[FlasherCommands.PRINTER_FIRWARE_UPDATE_STARTED].command);

                        Log.Information($"Waiting for '{_options.WaitLCD}' line");
                        var res = WaitFor(sp, _options.WaitLCD, 60);
                        if (!res.ok)
                        {
                            LoggingError("Aborted", res.allStrs);
                            return (FlasherCode.DISPLAY_NOT_READY, "");
                        }

                        Thread.Sleep(5000);

                        var hmiFirmware = z.GetEntry(_displayFileName);
                        if (hmiFirmware == null)
                        {
                            LoggingError("Can't find display firmware file: ", new List<string>() { _displayFileName });
                            return (FlasherCode.DISPLAY_FILE_INCORRECT, "");
                        }
                        //распаковываем файл прошивки экрана во временную папку
                        var hmiFirmwarePath = Path.Combine(tempDirectory, _displayFileName);
                        hmiFirmware.ExtractToFile(hmiFirmwarePath);
                        var fileBytes = File.ReadAllBytes(hmiFirmwarePath);

                        //сообщение что начинается прошивка экрана
                        sp.DiscardInBuffer();
                        Log.Information("Starting display firmware update");
                        sp.WriteLine($"{_commandAndAnswerPatterns[FlasherCommands.DISPLAY_FIRMWARE_UPDATE_STARTED].command} S{fileBytes.Length}");

                        var ok = GetChars(sp, 20, _commandAndAnswerPatterns[FlasherCommands.DISPLAY_FIRMWARE_UPDATE_STARTED].pattern);
                        Log.Information($"Received: '{ok.value}'");
                        if (ok.hasError || !ok.value.Equals("ok"))
                        {
                            for (var i = 0; i < 4; i++)
                            {
                                sp.DiscardInBuffer();
                                Log.Information($"Trying to start display firmware update again - repeat {i}");
                                sp.WriteLine($"{_commandAndAnswerPatterns[FlasherCommands.DISPLAY_FIRMWARE_UPDATE_STARTED].command} S{fileBytes.Length}");

                                ok = GetChars(sp, 10, _commandAndAnswerPatterns[FlasherCommands.DISPLAY_FIRMWARE_UPDATE_STARTED].pattern);
                                Log.Information($"Received: '{ok.value}'");
                                if (!ok.hasError && ok.value.Equals("ok")) break;
                            }

                            if (ok.hasError || !ok.value.Equals("ok"))
                            {
                                LoggingError("Can't start display firmware update", ok.allStrs);
                                return (FlasherCode.DISPLAY_NOT_READY, "");
                            }
                        }

                        sp.DiscardInBuffer();
                        Log.Information("Machine is ready for display firmware update");
                        UpdateStatus(FlasherStatus.DISPLAY_READY);

                        //передаем прошивку экрана пакетами по 128 байт
                        var packet = new byte[BUFFER_SIZE];
                        while (count < fileBytes.Length)
                        {
                            packet[packetPos++] = (byte)fileBytes[count];
                            if (packetPos == BUFFER_SIZE)
                            {
                                sp.Write(packet, 0, BUFFER_SIZE);
                                var resChars = GetChars(sp, 10, _commandAndAnswerPatterns[FlasherCommands.DISPLAY_FIRMWARING].pattern);
                                if (resChars.hasError || !resChars.value.Equals("ok"))
                                {
                                    LoggingError("Error during display firmware update", resChars.allStrs);
                                    Log.Information($"Sent {count} bytes out of {fileBytes.Length}");
                                    return (FlasherCode.DISPLAY_FIRMWARING_ERROR, "");
                                }
                                packetPos = 0;
                            }

                            count++;
                        }
                        Directory.Delete(tempDirectory, true);

                        //если остался неполный пакет - передаем его
                        if (packetPos > 0)
                        {
                            sp.Write(packet, 0, packetPos);
                            ok = GetChars(sp, 10, _commandAndAnswerPatterns[FlasherCommands.DISPLAY_FIRMWARING].pattern);
                            Log.Information($"Received: '{ok.value}'");
                            if (ok.hasError || !ok.value.Equals("ok"))
                            {
                                LoggingError("Error during last packet on display firmware update", ok.allStrs);
                                return (FlasherCode.DISPLAY_FIRMWARING_ERROR, "");
                            }
                        }

                        UpdateStatus(FlasherStatus.DISPLAY_UPDATED);
                        Log.Information($"Display update finished");
                    }
                    return (FlasherCode.SUCCESS, "");
                };
                var updateConfig = (Stream gc) => {
                    if (!string.IsNullOrEmpty(_options.WaitConfig)) {
                        Log.Information($"Waiting for '{_options.WaitConfig}' line");
                        var res = WaitFor(sp, _options.WaitConfig, 60);
                        if (!res.ok)
                        {
                            LoggingError("Aborted", res.allStrs);
                            return (FlasherCode.PRINTER_NOT_READY, "");
                        }
                    }
                    var streamReader = new StreamReader(gc);

                    // TODO: Fix config CRC calculation and saving mechanism
                    //~ var crc32Hasher = Crc32();
                    //~ var crc32 = crc32Hasher.HashToUInt32(streamReader);
                    //~ streamReader.DiscardBufferedData();
                    //~ streamReader.BaseStream.Seek(0, SeekOrigin.Begin);

                    var configVersionFromFile = streamReader.ReadLine().Substring(1);
                    var configCRCFromFile = streamReader.ReadLine().Substring(1);

                    if (configVersionFromFile != configVersionFromMachine.value || configCRCFromFile != configCRC.value)
                    {
                        Log.Information($"Config update needed, starting config update");
                        while (!streamReader.EndOfStream)
                        {
                            var str = streamReader.ReadLine();
                            if (str != null && str.Any() && str[0] != ';')
                            {
                                sp.DiscardInBuffer();
                                Log.Information($"Sending:{str}");
                                sp.WriteLine(str);

                                var (value, allStrs, hasError) = GetChars(sp, 10, "ok");
                                Log.Information($"Received:{value}");
                                if (hasError)
                                {
                                    LoggingError("Error while config update", allStrs);
                                    return (FlasherCode.CONFIG_WRITTING_ERROR, "");
                                }
                            }
                        }

                        sp.DiscardInBuffer();
                        Log.Information("Saving config to EEPROM");
                        sp.WriteLine(_commandAndAnswerPatterns[FlasherCommands.EEPROM_WRITING].command);

                        var ok = GetChars(sp, 10, _commandAndAnswerPatterns[FlasherCommands.EEPROM_WRITING].pattern);
                        Log.Information($"Received:{ok.value}");
                        if (ok.hasError || !ok.value.Equals("ok"))
                        {
                            LoggingError("Error while saving config to EEPROM", ok.allStrs);
                            return (FlasherCode.EEPROM_WRITTING_ERROR, "");
                        }
                    }

                    streamReader?.Close();
                    streamReader?.Dispose();
                    return (FlasherCode.SUCCESS, "");
                };

                if (!string.IsNullOrEmpty(_options.InputFileName)) {
                    using (var z = ZipFile.OpenRead(_options.InputFileName))
                    {
                        var (code, msg) = readArchive(z);
                        if (code != FlasherCode.SUCCESS) {
                            return (code, msg);
                        }
                        if (!string.IsNullOrEmpty(_options.PrinterConfig)) {
                            using (FileStream gc = File.OpenRead(_options.PrinterConfig))
                            {
                                (code, msg) = updateConfig(gc);
                                if (code != FlasherCode.SUCCESS) {
                                    return (code, msg);
                                }
                            }
                        } else if (!string.IsNullOrEmpty(printerFileNameTemplate)) {
                            var gc = z.GetEntry(printerFileNameTemplate+".gc");
                            using (var stream = gc.Open()) {
                                (code, msg) = updateConfig(stream);
                                if (code != FlasherCode.SUCCESS) {
                                    return (code, msg);
                                }
                            }
                        }
                    }
                } else {
                    if (_options.FlashMain)
                    {
                        LoggingError("No input file provided with --main flag", null);
                        return (FlasherCode.FLASHER_OPTIONS_INCORRECT, "");
                    }
                    if (_options.FlashLCD)
                    {
                        LoggingError("No input file provided with --lcd flag", null);
                        return (FlasherCode.FLASHER_OPTIONS_INCORRECT, "");
                    }

                    if (!string.IsNullOrEmpty(_options.PrinterConfig)) {
                        using (FileStream gc = File.OpenRead(_options.PrinterConfig))
                        {
                            var (code, msg) = updateConfig(gc);
                            if (code != FlasherCode.SUCCESS) {
                                return (code, msg);
                            }
                        }
                    }
                }

                if (devMode)
                {
                    var trueVersion = _options.PrinterModelVersion.Split('-').Last();
                    sp.DiscardInBuffer();
                    Log.Information("Saving printer version to EEPROM");
                    sp.WriteLine(_commandAndAnswerPatterns[FlasherCommands.SAVE_PRINTER_VERSION_TO_EEPROM].command+" "+trueVersion);

                    var ok = GetChars(sp, 10, _commandAndAnswerPatterns[FlasherCommands.SAVE_PRINTER_VERSION_TO_EEPROM].pattern);
                    Log.Information($"Received:{ok.value}");
                    if (ok.hasError || !ok.value.Equals("ok"))
                    {
                        LoggingError("Error saving printer version to EEPROM", ok.allStrs);
                        return (FlasherCode.EEPROM_WRITTING_ERROR, "");
                    }
                }

                UpdateStatus(FlasherStatus.EEPROM_UPDATED);
                Log.Information("Printer configuration updated");
                Log.Information("Rebooting printer");
                //небольшой лайфхак для перезапуска серийного порта
                sp.Close();
                Thread.Sleep(1000);
                sp.Open();
                Thread.Sleep(1000);
                sp.Close();
                Log.Information("Firmware update successfully finished");
                return (FlasherCode.SUCCESS, "");
            }
            catch (Exception e)
            {
                Log.Information($"Exception: {e.Message}");
                //если есть ошибка, то возвращаем и ее текст
                return (FlasherCode.UNKNOWN_ERROR, e.Message);
            }

            finally
            {
                if (sp != null && sp.IsOpen) sp.Close();
                process?.Close();
                process?.Dispose();
                if (tempDirectory != null && Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
        }

        private void LoggingError(string message, List<string> allStrs)
        {
            Log.Information($"{message}\n{string.Join("", allStrs)}");
            Log.Information("--------------------------------------");
        }

        private void UpdateStatus(FlasherStatus code)
        {
            Updated?.Invoke(this, code);
        }

        private (string value, List<string> allStrs, bool hasError) GetChars(SerialPort sp, int secondToWait, string pattern)
        {
            var hasElapsed = false;
            //var timer = new Timer(state => hasElapsed = true, null, secondToWait * 1000, 0);
            var timer = new Timer(secondToWait * 1000);
            timer.Elapsed += (sender, args) =>
            {
                hasElapsed = true;
                (sender as Timer).Stop();
            };
            timer.Start();
            var chars = new List<char>();
            var allstrs = new List<string>();
            while (!hasElapsed)
            {
                if (sp.BytesToRead > 0)
                {
                    var c = (char)sp.ReadChar();
                    if (c == '\n')
                    {
                        var rawString = new string(chars.ToArray()).TrimEnd('\r', '\n');
                        if (_options.Verbose) {
                            Log.Information($"GET >> {rawString}");
                        }
                        allstrs.Add(rawString);
                        if (rawString.StartsWith(pattern))
                        {
                            return (rawString.Substring(pattern.Length), allstrs, false);
                        }

                        chars.Clear();
                    }
                    else
                    {
                        chars.Add(c);
                    }
                }
            }
            return (null, allstrs, hasElapsed);
        }

        private (bool ok, List<string> allStrs) WaitFor(SerialPort sp, string wait, int secondToWait)
        {
            var hasElapsed = false;
            //var timer = new Timer(state => hasElapsed = true, null, secondToWait * 1000, 0);
            var timer = new Timer(secondToWait * 1000);
            timer.Elapsed += (sender, args) =>
            {
                hasElapsed = true;
                (sender as Timer).Stop();
            };
            timer.Start();
            var chars = new List<char>();
            var strings = new List<string>();
            while (!hasElapsed)
            {
                if (sp.BytesToRead > 0)
                {
                    var c = (char)sp.ReadChar();
                    if (c == '\n')
                    {
                        var strIn = new string(chars.ToArray()).TrimEnd('\r', '\n');
                        if (_options.Verbose) {
                            Log.Information($"WAIT >> {strIn}");
                        }
                        strings.Add(strIn);
                        if (wait.Equals(strIn))
                        {
                            return (true, strings);
                        }
                        chars.Clear();
                    }
                    else
                    {
                        chars.Add(c);
                    }
                }
            }

            return (false, strings);
        }
    }
}