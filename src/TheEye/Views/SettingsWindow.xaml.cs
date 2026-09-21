using System.Windows;
using TheEye.Core;

namespace TheEye.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        Settings = SettingsSerializer.Deserialize(SettingsSerializer.Serialize(settings));
        DataContext = Settings;
    }

    public AppSettings Settings { get; }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (HasValidationError(this))
            {
                System.Windows.MessageBox.Show(this, "Enter valid numbers for work and rest durations.", "TheEye Settings");
                return;
            }
            IsEnabled = false;
            await App.CurrentApp.ApplySettingsAsync(Settings);
            Close();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Settings could not be saved: {ex.Message}", "TheEye Settings");
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private void PreviewButton_Click(object sender, RoutedEventArgs e) => App.CurrentApp.OpenAnimationPreview(Settings);

    private static bool HasValidationError(DependencyObject element)
    {
        if (System.Windows.Controls.Validation.GetHasError(element)) return true;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); i++)
            if (HasValidationError(System.Windows.Media.VisualTreeHelper.GetChild(element, i))) return true;
        return false;
    }
}
