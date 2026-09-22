using System.Drawing;
using System.Windows.Forms;
using TheEye.Core;

namespace TheEye.Services;

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
        menu.Items.Add(new ToolStripMenuItem("Open TheEye", null, (_, _) => open()));
        menu.Items.Add(_startItem);
        menu.Items.Add(_restItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Settings", null, (_, _) => settings()));
        menu.Items.Add(new ToolStripMenuItem("Animation Preview", null, (_, _) => preview()));
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => exit()));

        // Load the packaged icon directly so Explorer's executable-icon cache
        // cannot keep an older character icon in the notification area.
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "TheEye.ico");
        var applicationIcon = System.IO.File.Exists(iconPath) ? new Icon(iconPath) : null;
        _notifyIcon = new NotifyIcon
        {
            Icon = applicationIcon ?? SystemIcons.Information,
            Text = "TheEye",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) open();
        };
    }

    public void Update(SessionState state)
    {
        _startItem.Enabled = state == SessionState.Idle;
        _restItem.Enabled = state == SessionState.Working;
        _notifyIcon.Text = state switch
        {
            SessionState.Working => "TheEye — focusing",
            SessionState.MandatoryRestLocked => "TheEye — rest in progress",
            SessionState.VoluntaryRest => "TheEye — resting",
            _ => "TheEye"
        };
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
