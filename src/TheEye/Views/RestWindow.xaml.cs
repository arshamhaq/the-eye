using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheEye.Core;
using TheEye.ViewModels;

namespace TheEye.Views;

public partial class RestWindow : Window
{
    private readonly SessionManager _session;
    private bool _locked;
    private bool _placed;
    public RestWindow(SessionManager session, System.Windows.Media.ImageSource? petImage)
    {
        InitializeComponent();
        _session = session;
        PetImage.Source = petImage ?? throw new InvalidOperationException("The rest artwork is missing from this installation.");
        Loaded += (_, _) => { Services.DisplayGeometry.CoverMonitor(this); PlaceButton(); };
        SizeChanged += (_, _) => { if (IsLoaded) PlaceButton(); };
    }
    public bool AllowClose { get; set; }
    public void SetEmergencyTickets(int remaining)
    {
        EmergencyTicketButton.Content = $"Use emergency ticket ({remaining} left)";
        EmergencyTicketButton.IsEnabled = remaining > 0;
    }
    private void EmergencyTicketButton_Click(object sender, RoutedEventArgs e) => App.CurrentApp.RequestEmergencyExit();
    public void Update(SessionSnapshot snapshot)
    {
        var wasLocked = _locked;
        _locked = snapshot.State == SessionState.MandatoryRestLocked;
        CountdownText.Text = _locked ? MainViewModel.FormatRemaining(snapshot.Remaining)
            : snapshot.State == SessionState.VoluntaryRest ? "Take your time" : "Ready when you are";
        CountdownText.FontSize = _locked ? 115 : 64;
        LockedText.Text = _locked ? "The button needs a break too. Catch it when the timer ends." : "Press Rested to begin a fresh work session.";
        RestedButton.Focusable = !_locked;
        RestedButton.IsTabStop = !_locked;
        if (wasLocked && !_locked) PlaceButton();
    }
    private void PlaceButton()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        Canvas.SetLeft(RestedButton, ActualWidth * 0.12);
        Canvas.SetTop(RestedButton, ActualHeight * 0.80);
        _placed = true;
    }
    private void Dodge(System.Windows.Point pointer)
    {
        if (!_locked || !_placed) return;
        var maxX = Math.Max(20, ActualWidth - RestedButton.Width - 30);
        var maxY = Math.Max(20, ActualHeight - RestedButton.Height - 30);
        var candidates = new[] { new System.Windows.Point(30,30), new System.Windows.Point(maxX,30),
            new System.Windows.Point(30,maxY), new System.Windows.Point(maxX,maxY), new System.Windows.Point(maxX/2,maxY/2) };
        var destination = candidates.OrderByDescending(p => (p + new Vector(90, 26) - pointer).LengthSquared).First();
        Canvas.SetLeft(RestedButton, destination.X);
        Canvas.SetTop(RestedButton, destination.Y);
    }
    private void Window_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_locked || !_placed) return;
        var p = e.GetPosition(ButtonLayer);
        var bounds = new Rect(Canvas.GetLeft(RestedButton) - 45, Canvas.GetTop(RestedButton) - 45, RestedButton.Width + 90, RestedButton.Height + 90);
        if (bounds.Contains(p)) Dodge(p);
    }
    private void RestedButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => Dodge(e.GetPosition(ButtonLayer));
    private void RestedButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_locked) return;
        e.Handled = true;
        Dodge(e.GetPosition(ButtonLayer));
    }
    private void RestedButton_PreviewTouchDown(object sender, TouchEventArgs e)
    {
        if (!_locked) return;
        e.Handled = true;
        Dodge(e.GetTouchPoint(ButtonLayer).Position);
    }
    private void RestedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session.State == SessionState.MandatoryRestLocked) { Dodge(System.Windows.Input.Mouse.GetPosition(ButtonLayer)); return; }
        _session.CompleteRest();
    }
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape || (_locked && e.Key is Key.Enter or Key.Space)) e.Handled = true;
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose) e.Cancel = true;
        base.OnClosing(e);
    }
}
