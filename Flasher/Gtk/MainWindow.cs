using System;
using Gtk;
using Eto.Forms;
using FlasherGUI;

namespace FlasherGtk
{

  public class MainWindow: Gtk.Window
  {
    public MainWindow() : base(Gtk.WindowType.Toplevel)
    {
      Title = FlasherPanel.Title;
      WindowPosition = Gtk.WindowPosition.CenterOnParent;
      DefaultWidth = FlasherPanel.DefaultWidth;
      DefaultHeight = FlasherPanel.DefaultHeight;
      DeleteEvent += OnDeleteEvent;

      var nativeWidget = new FlasherPanel().ToNative(true);

      Child = nativeWidget;
    }

    protected void OnDeleteEvent(object sender, DeleteEventArgs a)
    {
      Gtk.Application.Quit();
      a.RetVal = true;
    }
  }
}
