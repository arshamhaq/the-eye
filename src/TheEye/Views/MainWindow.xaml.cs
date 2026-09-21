using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace TheEye.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
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
