using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EyeDragon.Core;
using EyeDragon.ViewModels;
using Forms = System.Windows.Forms;

namespace EyeDragon.Views;

public partial class RestWindow : Window
{
    private readonly SessionManager _session;

    public RestWindow(SessionManager session, System.Windows.Media.ImageSource? petImage)
    {
        InitializeComponent();
        _session = session;
        PetImage.Source = petImage;
        Loaded += (_, _) => CoverCurrentScreen();
    }

    public bool AllowClose { get; set; }

    public void Update(SessionSnapshot snapshot)
    {
        var locked = snapshot.State == SessionState.MandatoryRestLocked;
        CountdownText.Text = locked ? MainViewModel.FormatRemaining(snapshot.Remaining) : "Rest complete";
        CountdownText.FontSize = locked ? 62 : 38;
        LockedText.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        RestedButton.Visibility = snapshot.State == SessionState.MandatoryRestComplete
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    private void RestedButton_Click(object sender, RoutedEventArgs e) => _session.CompleteRest();

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
        }
    }

    private void CoverCurrentScreen()
    {
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = screen.Bounds.Left / dpi.DpiScaleX;
        Top = screen.Bounds.Top / dpi.DpiScaleY;
        Width = screen.Bounds.Width / dpi.DpiScaleX;
        Height = screen.Bounds.Height / dpi.DpiScaleY;
    }
}
