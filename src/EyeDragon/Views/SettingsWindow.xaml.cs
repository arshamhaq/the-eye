using System.Windows;
using EyeDragon.Core;

namespace EyeDragon.Views;

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
        await App.CurrentApp.ApplySettingsAsync(Settings);
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void PreviewButton_Click(object sender, RoutedEventArgs e) => App.CurrentApp.OpenAnimationPreview();
}
