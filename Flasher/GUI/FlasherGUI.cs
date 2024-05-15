using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Ports;
using Eto.Forms;
using Eto.Drawing;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Configuration;

namespace FlasherGUI
{
  public class TextAreaSink : ILogEventSink
  {
    private TextArea _textArea;
    private readonly IFormatProvider _formatProvider;

    public TextAreaSink(TextArea textArea, IFormatProvider formatProvider)
    {
      _textArea = textArea;
      _formatProvider = formatProvider;
    }

    public void Emit(LogEvent logEvent)
    {
      var message = logEvent.RenderMessage(_formatProvider);
      _textArea.Append(DateTimeOffset.Now.ToString() + " "  + message + "\r\n", true);
    }
  }

  public static class TextAreaSinkExtensions
  {
    public static LoggerConfiguration TextAreaSink(this LoggerSinkConfiguration loggerConfiguration, TextArea textArea, IFormatProvider formatProvider = null)
    {
      return loggerConfiguration.Sink(new TextAreaSink(textArea, formatProvider));
    }
  }

  public class FlashCommand : Command
  {
    public FilePicker FirmwareArchivePicker { get; }
    public FilePicker ConfigOverridePicker { get; }
    public CheckBox FlashMainCheckBox { get; }
    public CheckBox FlashLCDCheckBox { get; }
    public CheckBox UpdateConfigCheckBox { get; }
    public DropDown ConfigsDropDown { get; }
    public DropDown PortsDropDown { get; }
    public NumericStepper LCDBaudRate { get; }
    public TextBox PrinterModelVersion { get; }
    public TextArea FlasherLogs { get; }

    public FlashCommand()
    {
      FirmwareArchivePicker = new FilePicker{ Title = "Select firmware archive" };
      FirmwareArchivePicker.FilePathChanged += (s, e) => {
        if (!string.IsNullOrEmpty(FirmwareArchivePicker.FilePath)) {
          ConfigOverridePicker.FilePath = null;
          ListConfigFilesInArchive(FirmwareArchivePicker.FilePath);
        }
      };

      ConfigOverridePicker = new FilePicker{ Title = "Select config file" };
      ConfigOverridePicker.FilePathChanged += (s, e) => {
        if (!string.IsNullOrEmpty(ConfigOverridePicker.FilePath)) {
          ConfigsDropDown.DataStore = new List<string> {ConfigOverridePicker.FilePath};
          ConfigsDropDown.SelectedIndex = 0;
          UpdateConfigCheckBox.Checked = true;
        }
      };

      FlashMainCheckBox = new CheckBox{ Text = "Flash Main Board" };
      FlashLCDCheckBox = new CheckBox{ Text = "Flash LCD" };
      UpdateConfigCheckBox = new CheckBox{ Text = "Update config" };

      var configs = new List<string>();
      ConfigsDropDown = new DropDown{ DataStore = configs };
      ConfigsDropDown.SelectedValueChanged += (s, e) => {
        if (!string.IsNullOrEmpty(ConfigsDropDown.SelectedKey)) {
          var name = Path.GetFileNameWithoutExtension(ConfigsDropDown.SelectedKey);
          if (name.StartsWith("Composer ")) {
            PrinterModelVersion.Text = name;
          }
        }
      };

      var ports = SerialPort.GetPortNames();
      PortsDropDown = new DropDown{ DataStore = ports };

      LCDBaudRate = new NumericStepper { Value = 1200, MinValue = 1200, MaxValue = 115200, DecimalPlaces = 0, Increment = 100 };
      PrinterModelVersion = new TextBox();
      FlasherLogs = new TextArea { ReadOnly = true, Width = 900, Height = 130 };
    }

    protected override void OnExecuted(EventArgs e)
    {
      base.OnExecuted(e);
      try {
        var messages = new Queue<string>();

        Log.Logger = new LoggerConfiguration()
          .MinimumLevel.Information()
          .WriteTo.TextAreaSink(FlasherLogs)
          .CreateLogger();

        Log.Information($"Flashing {PrinterModelVersion.Text} on {PortsDropDown.SelectedKey}");

        var options = new Flasher.Options(
          PortsDropDown.SelectedKey,
          PrinterModelVersion.Text,
          FirmwareArchivePicker.FilePath,
          FlashMainCheckBox.Checked.Value,
          FlashLCDCheckBox.Checked.Value,
          UpdateConfigCheckBox.Checked.Value ? ConfigOverridePicker.FilePath : null,
          "Status:I",
          "Status:I",
          115200,
          (int)LCDBaudRate.Value,
          false
        );
        var flasher = new Flasher.Flasher(options);
        var result = flasher.Flash();
        MessageBox.Show(Application.Instance.MainForm, $"Flashed: {result}", "Flashing DONE", MessageBoxButtons.OK);
      } catch (Exception ex) {
        MessageBox.Show(Application.Instance.MainForm, ex.Message, "Flashing FAILED", MessageBoxButtons.OK);
        return;
      }
    }

    private void ListConfigFilesInArchive(string path) {
      var entries = new List<string>();
      using (var z = ZipFile.OpenRead(path)) {
        foreach (var e in z.Entries) {
          if (e.Name.StartsWith("Composer ")) {
            entries.Add(e.Name);
          }
        }
      }
      var configs = string.Join(", ", entries);
      ConfigsDropDown.DataStore = entries;
      ConfigsDropDown.SelectedIndex = entries.Count - 1;
      FlashMainCheckBox.Checked = true;
      UpdateConfigCheckBox.Checked = true;
    }
  }

  /// <summary>
  /// Eto.Forms panel to embed in an existing WinForms app
  /// </summary>
  public class FlasherPanel : Panel
  {
    public static string Title = "Anisoprint Composer Flasher";
    public static int DefaultWidth = 800;
    public static int DefaultHeight = 250;

    private static FlashCommand _flashCommand;

    public FlasherPanel() {
      _flashCommand = new FlashCommand();

      Content = new StackLayout(
        new StackLayoutItem("Flash"),
        new StackLayoutItem(new TableLayout(
          new TableRow(
            new Label { Text = "COM port", VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center },
            _flashCommand.PortsDropDown,
            null
          ),
          new TableRow(
            new Label { Text = "Firmware archive", VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center },
            new TableCell(_flashCommand.FirmwareArchivePicker, true),
            _flashCommand.FlashMainCheckBox,
            null
          ),
          new TableRow(
            new Label { Text = "Config file override", VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center },
            new TableCell(_flashCommand.ConfigOverridePicker, true),
            _flashCommand.FlashLCDCheckBox,
            new Label { Text = "LCD BAUD rate: ", VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right  },
            _flashCommand.LCDBaudRate,
            null
          ),
          new TableRow(
            new Label { Text = "Select config file", VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center  },
            _flashCommand.ConfigsDropDown,
            _flashCommand.UpdateConfigCheckBox,
            null
          ),
          new TableRow(
            new Label { Text = "Printer model & version", VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center  },
            _flashCommand.PrinterModelVersion,
            new Button { Text = "Flash!", Command = _flashCommand },
            null
          ),
          null
        )),
        new StackLayoutItem("Log"),
        new StackLayoutItem(_flashCommand.FlasherLogs, HorizontalAlignment.Left, true)
      );
    }
  }
}
