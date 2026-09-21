using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace TheEye.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) =>
        {
            var gutter = WindowState == WindowState.Maximized ? 0 : 24;
            WindowHalo.Margin = WindowSurface.Margin = new Thickness(gutter);
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Services.DisplayGeometry.UseWorkingAreaWhenMaximized(this);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            if (e.ClickCount == 2) { ToggleMaximize(); return; }
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Preview_Click(object sender, RoutedEventArgs e) => App.CurrentApp.OpenAnimationPreview();
    private void ReturnToRest_Click(object sender, RoutedEventArgs e) => App.CurrentApp.ShowRestWindow();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!App.CurrentApp.IsExiting)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
