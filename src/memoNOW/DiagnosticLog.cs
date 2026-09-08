using System.IO;
using System.Text;

namespace memoNOW;

public static class DiagnosticLog
{
    private static readonly object Sync = new();

    public static string LogDirectory
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "memoNOW", "logs");
        }
    }

    public static string CurrentLogPath => Path.Combine(LogDirectory, "memoNOW.log");

    public static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(
                    CurrentLogPath,
                    $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never become another reason for memoNOW to fail.
        }
    }

    public static void WriteException(string context, Exception exception)
    {
        Write($"ERROR {context}: {exception.GetType().FullName}: {exception.Message}{Environment.NewLine}{exception.StackTrace}");

        if (exception.InnerException is not null)
        {
            Write($"INNER {context}: {exception.InnerException.GetType().FullName}: {exception.InnerException.Message}{Environment.NewLine}{exception.InnerException.StackTrace}");
        }
    }
}
