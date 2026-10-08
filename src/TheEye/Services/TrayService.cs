using System.Drawing;
using System.Windows.Forms;
using TheEye.Core;

namespace TheEye.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _startItem;
    private readonly ToolStripMenuItem _gamingItem;
    private readonly ToolStripMenuItem _restItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly ToolStripMenuItem _ticketInfo;

    public TrayService(Action open, Action start, Action startGaming, Action rest, Action settings, Action preview, Action exit, Action refresh)
    {
        _startItem = new ToolStripMenuItem("Start Working", null, (_, _) => start());
        _gamingItem = new ToolStripMenuItem("Start Gaming", null, (_, _) => startGaming());
        _restItem = new ToolStripMenuItem("Resting Now", null, (_, _) => rest());
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Open TheEye", null, (_, _) => open()));
        menu.Items.Add(_startItem);
        menu.Items.Add(_gamingItem);
        menu.Items.Add(_restItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Settings", null, (_, _) => settings()));
        menu.Items.Add(new ToolStripMenuItem("Animation Preview", null, (_, _) => preview()));
        _ticketInfo = new ToolStripMenuItem("Emergency tickets: 3/3 (reset Monday)") { Enabled = false };
        _exitItem = new ToolStripMenuItem("Exit", null, (_, _) => exit());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_ticketInfo);
        menu.Items.Add(_exitItem);
        menu.Opening += (_, _) => refresh();

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

    public void Update(SessionState state, bool committed = false, int ticketsRemaining = 3, SessionMode mode = SessionMode.Working)
    {
        _ticketInfo.Text = $"Emergency tickets: {ticketsRemaining}/3 (reset Monday)";
        _exitItem.Text = committed ? $"Use emergency ticket ({ticketsRemaining} left)" : "Exit";
        _exitItem.Enabled = !committed || ticketsRemaining > 0;
        _startItem.Enabled = state == SessionState.Idle;
        _gamingItem.Enabled = state == SessionState.Idle;
        _restItem.Enabled = state == SessionState.Working;
        _notifyIcon.Text = state switch
        {
            SessionState.Working => mode == SessionMode.Gaming ? "TheEye — gaming" : "TheEye — focusing",
            SessionState.MandatoryRestLocked => "TheEye — rest in progress",
            SessionState.VoluntaryRest => "TheEye — resting",
            _ => "TheEye"
        };
    }

    public void ShowNotice(string message) => _notifyIcon.ShowBalloonTip(5000, "TheEye", message, ToolTipIcon.Info);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
