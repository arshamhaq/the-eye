using System.IO;
using System.Windows.Media;

namespace TheEye.Services;

public sealed class SoundService : IDisposable
{
    private readonly MediaPlayer _warningPlayer = CreatePlayer("warning.wav");
    private readonly MediaPlayer _restPlayer = CreatePlayer("rest-start.wav");

    public void PlayWarning(double volume) => Play(_warningPlayer, volume);

    public void PlayRestStart(double volume) => Play(_restPlayer, volume);

    public void Dispose()
    {
        _warningPlayer.Close();
        _restPlayer.Close();
    }

    private static MediaPlayer CreatePlayer(string fileName)
    {
        var player = new MediaPlayer();
        player.Open(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", fileName), UriKind.Absolute));
        return player;
    }

    private static void Play(MediaPlayer player, double volume)
    {
        player.Volume = Math.Clamp(volume, 0, 1);
        player.Position = TimeSpan.Zero;
        player.Play();
    }
}
