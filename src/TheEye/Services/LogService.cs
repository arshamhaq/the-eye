using System.IO;

namespace TheEye.Services;

public sealed class LogService
{
    private const long MaximumLogBytes = 512 * 1024;
    private readonly string _logPath;
    private readonly object _sync = new();

    public LogService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TheEye");
        Directory.CreateDirectory(directory);
        _logPath = Path.Combine(directory, "theeye.log");
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception exception) =>
        Write("ERROR", $"{message}: {exception.GetType().Name}: {exception.Message}");

    private void Write(string level, string message)
    {
        lock (_sync)
        {
            try
            {
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length > MaximumLogBytes)
                {
                    File.Move(_logPath, _logPath + ".previous", true);
                }

                File.AppendAllText(
                    _logPath,
                    $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
    }
}
