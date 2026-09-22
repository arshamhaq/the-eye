using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using TheEye.Core;
using TheEye.ViewModels;
using TheEye.Views;

// Off-screen rendering only: no App.Run/OnStartup, ledger, tray, timer or
// startup registration. Safe to execute alongside an active user commitment.
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app = new TheEye.App();
        app.InitializeComponent();
        var output = Path.GetFullPath("artifacts/button-rendering");
        Directory.CreateDirectory(output);
        BitmapImage Load(string name) => new(new Uri(Path.GetFullPath("src/TheEye/Pets/Triangle/Assets/" + name)));
        var session = new SessionManager(new SystemClock());
        var results = new List<string>();
        Render(new MainWindow { DataContext = new MainViewModel(session, Load("mountains.png"), Load("float.png")) }, "main");
        var rest = new RestWindow(session, Load("resting.png"));
        rest.Update(new SessionSnapshot(SessionState.MandatoryRestLocked, TimeSpan.FromMinutes(3), false, false, false));
        Render(rest, "rest");
        Render(new SettingsWindow(new AppSettings()), "settings");
        File.WriteAllLines(Path.Combine(output, "results.txt"), results);
        Console.WriteLine(string.Join(Environment.NewLine, results));
        return 0;

        void Render(Window window, string name)
        {
            var root = (FrameworkElement)window.Content;
            root.DataContext = window.DataContext;
            window.Content = null;
            using var source = new HwndSource(new HwndSourceParameters("TheEye off-screen rendering check")
            {
                Width = 1920, Height = 1080,
                WindowStyle = unchecked((int)0x80000000) // popup WITHOUT WS_VISIBLE
            });
            source.RootVisual = root;
            var dpi = VisualTreeHelper.GetDpi(root);
            var size = new Size(1920 / dpi.DpiScaleX, 1080 / dpi.DpiScaleY);
            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();
            if (name == "rest")
            {
                var rested = Descendants(root).OfType<Button>().Single(button => button.Name == "RestedButton");
                Canvas.SetLeft(rested, size.Width * 0.12);
                Canvas.SetTop(rested, size.Height * 0.8);
                root.UpdateLayout();
            }
            var buttons = Descendants(root).OfType<Button>().ToArray();
            foreach (var button in buttons)
            {
                button.ApplyTemplate();
                var content = (ContentPresenter)button.Template.FindName("ButtonContent", button);
                var glow = (Border)button.Template.FindName("ButtonGlow", button);
                if (glow.Effect is not DropShadowEffect || glow.Child is not null)
                    throw new InvalidOperationException("Glow must be isolated on an empty backing shape.");
                for (DependencyObject? current = content; current is not null; current = VisualTreeHelper.GetParent(current))
                    if (current is UIElement { Effect: not null })
                        throw new InvalidOperationException("Button label is still rendered inside an effect surface.");
                if (!button.UseLayoutRounding || !button.SnapsToDevicePixels ||
                    TextOptions.GetTextFormattingMode(button) != TextFormattingMode.Display)
                    throw new InvalidOperationException("Button is missing native DPI text/layout settings.");
            }
            var bitmap = new RenderTargetBitmap(1920, 1080, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png"));
            encoder.Save(stream);
            results.Add($"PASS {name}: {buttons.Length} buttons; sharp text outside glow effects; 1920x1080 at {dpi.PixelsPerInchX} DPI");
            source.RootVisual = null;
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
