using System.IO;
namespace SavefileManager.Services;

/// <summary>Very small file logger (config/log.log). Never throws.</summary>
public static class Log
{
    private static string? _path;
    private static readonly object Gate = new();

    public static void Init(string path)
    {
        _path = path;
        try
        {
            // Keep the log from growing forever: start fresh once it passes 1 MB.
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000)
                File.Move(path, path + ".old", true);
        }
        catch { /* ignore */ }
    }

    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARNING", msg);
    public static void Error(string msg) => Write("ERROR", msg);

    private static void Write(string level, string msg)
    {
        if (_path == null) return;
        try
        {
            lock (Gate)
                File.AppendAllText(_path, $"{level} {DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
        }
        catch { /* ignore */ }
    }
}
