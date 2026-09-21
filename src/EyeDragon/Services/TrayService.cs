using System.Drawing;
using System.Windows.Forms;
using EyeDragon.Core;

namespace EyeDragon.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _startItem;
    private readonly ToolStripMenuItem _restItem;

    public TrayService(Action open, Action start, Action rest, Action settings, Action preview, Action exit)
    {
        _startItem = new ToolStripMenuItem("Start Working", null, (_, _) => start());
        _restItem = new ToolStripMenuItem("Resting Now", null, (_, _) => rest());
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Open EyeDragon", null, (_, _) => open()));
        menu.Items.Add(_startItem);
        menu.Items.Add(_restItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Settings", null, (_, _) => settings()));
        menu.Items.Add(new ToolStripMenuItem("Animation Preview", null, (_, _) => preview()));
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => exit()));

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "EyeDragon",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => open();
    }

    public void Update(SessionState state)
    {
        _startItem.Enabled = state == SessionState.Idle;
        _restItem.Enabled = state == SessionState.Working;
        _notifyIcon.Text = state switch
        {
            SessionState.Working => "EyeDragon — focusing",
            SessionState.MandatoryRestLocked => "EyeDragon — rest in progress",
            SessionState.VoluntaryRest => "EyeDragon — resting",
            _ => "EyeDragon"
        };
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
