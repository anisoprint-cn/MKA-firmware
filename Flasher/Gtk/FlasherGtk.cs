using System;
using Gtk;

namespace FlasherGtk
{
  static class Program
  {
    public static MainWindow MainWindow { get; set; }

    [STAThread]
    public static void Main(string[] args)
    {
      Application.Init();
      new Eto.Forms.Application(new Eto.GtkSharp.Platform()).Attach();
      MainWindow = new MainWindow();
      MainWindow.Child.Show();
      Application.Run();
    }
  }
}
