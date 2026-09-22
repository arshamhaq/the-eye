using System.IO;
using System.Windows;
using TheEye.Core;

namespace TheEye;

public partial class App
{
    private FocusGuard? _focusGuard;
    private bool _verificationMode;
    private bool _allowDiagnostics;
    private bool _endingWindowsSession;
    private long _lastCheckpoint;
    private SessionState _checkpointState = SessionState.Idle;
    private bool _checkpointSuspended;
    private string? _guardFailure;
    private bool _checkpointErrorReported;

    private void InitializeFocusGuard(string[] arguments)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TheEye", "commitment.json");
            string? logonId = null;
            try { logonId = Services.WindowsLogonIdentity.Read(); }
            catch (System.ComponentModel.Win32Exception ex) { _log?.Error("Could not read Windows logon identity", ex); }
            _focusGuard = new FocusGuard(path, windowsLogonId: logonId);
            _allowDiagnostics = !_focusGuard.IsCommitted;
            _verificationMode = _allowDiagnostics && arguments.Any(a => a.StartsWith("--verify-ui=", StringComparison.OrdinalIgnoreCase));
            if (_verificationMode)
            {
                // Tests never spend real tickets or clear a user's commitment.
                var temporary = Path.Combine(Path.GetTempPath(), "TheEye-verification-" + Guid.NewGuid().ToString("N"), "commitment.json");
                _focusGuard = new FocusGuard(temporary);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _guardFailure = ex.Message;
            _log?.Error("Cannot load commitment ledger; refusing to reset tickets", ex);
        }
    }

    private bool PersistFirstStart()
    {
        try
        {
            if (_focusGuard is null) throw new IOException(_guardFailure ?? "Emergency ticket storage is unavailable.");
            if (_focusGuard.IsCommitted) return false;
            _focusGuard.BeginWork(_session!.Options);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show("Working was not started because the commitment could not be saved.\n\n" + ex.Message,
                "TheEye", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void SaveCommitmentSnapshot(SessionSnapshot snapshot)
    {
        if (_endingWindowsSession || IsExiting || _focusGuard is null || snapshot.State == SessionState.Idle) return;
        var uptime = Environment.TickCount64;
        if (snapshot.State == _checkpointState && snapshot.IsSuspended == _checkpointSuspended &&
            uptime - _lastCheckpoint < 5000) return;
        try
        {
            _focusGuard.Checkpoint(snapshot, _session!.Options);
            _lastCheckpoint = uptime;
            _checkpointState = snapshot.State;
            _checkpointSuspended = snapshot.IsSuspended;
            _checkpointErrorReported = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!_checkpointErrorReported)
            {
                _log?.Error("Could not checkpoint commitment", ex);
                _tray?.ShowNotice("TheEye could not save the latest timer checkpoint. The current session remains active.");
                _checkpointErrorReported = true;
            }
        }
    }

    private void RefreshCommitmentUi()
    {
        var remaining = 0;
        try { remaining = _focusGuard?.TicketsRemaining ?? 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Error("Could not refresh weekly tickets", ex);
        }
        _tray?.Update(_session?.State ?? SessionState.Idle, _focusGuard?.IsCommitted == true, remaining);
        _restWindow?.SetEmergencyTickets(remaining);
    }

    private void RequestTrayExit()
    {
        if (_focusGuard?.IsCommitted == true) RequestEmergencyExit();
        else RequestExit();
    }

    public void RequestEmergencyExit()
    {
        if (_focusGuard is null || !_focusGuard.IsCommitted) { RequestExit(); return; }
        try
        {
            var remaining = _focusGuard.TicketsRemaining;
            if (remaining == 0)
            {
                ShowEmergencyMessage($"No emergency tickets remain. Your next three tickets arrive on {_focusGuard.NextReset:yyyy-MM-dd}.\n\nYou can still shut down or restart Windows.", MessageBoxButton.OK);
                return;
            }
            var result = ShowEmergencyMessage($"Use one emergency ticket and exit TheEye?\n\n{remaining} of 3 remain this week; {remaining - 1} will remain after exiting.\nResets Monday, {_focusGuard.NextReset:yyyy-MM-dd}.\n\nCancel keeps your ticket and session.", MessageBoxButton.OKCancel);
            if (result != MessageBoxResult.OK) return;
            if (!_focusGuard.TryUseEmergencyTicket()) { RefreshCommitmentUi(); return; }
            _log?.Info("Emergency ticket spent; commitment released");
            ExitApplication();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Error("Emergency ticket transaction failed", ex);
            ShowEmergencyMessage("The ticket could not be saved, so TheEye has not exited.\n\n" + ex.Message, MessageBoxButton.OK);
        }
    }

    private MessageBoxResult ShowEmergencyMessage(string message, MessageBoxButton buttons)
    {
        var owner = _restWindow is { IsVisible: true } ? (Window)_restWindow :
            _mainWindow is { IsVisible: true } ? _mainWindow : null;
        var defaultResult = buttons == MessageBoxButton.OKCancel ? MessageBoxResult.Cancel : MessageBoxResult.OK;
        return owner is null
            ? System.Windows.MessageBox.Show(message, "TheEye — Emergency ticket", buttons, MessageBoxImage.Information, defaultResult)
            : System.Windows.MessageBox.Show(owner, message, "TheEye — Emergency ticket", buttons, MessageBoxImage.Information, defaultResult);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        // Never block Windows shutdown/logoff. Do not release on this query:
        // another application/user can still cancel the Windows shutdown.
        _endingWindowsSession = true;
        try
        {
            if (e.ReasonSessionEnding == ReasonSessionEnding.Shutdown)
                _focusGuard?.NoteShutdownRequest();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Error("Could not clear shutdown checkpoint", ex);
        }
        IsExiting = true;
        if (_restWindow is not null) _restWindow.AllowClose = true;
        e.Cancel = false;
        base.OnSessionEnding(e);
    }

    private void ExitApplication()
    {
        IsExiting = true;
        if (_restWindow is not null)
        {
            _restWindow.AllowClose = true;
            _restWindow.Close();
        }
        Shutdown();
    }
}
