using System.Collections.Concurrent;
using System.Data.SqlTypes;

namespace RCS;

/// <summary>
/// 
/// </summary>
public static class Logger
{
    static readonly BlockingCollection<string> _queue = new(); // Begone Async!
    
    static string _activeLoggingFile = "";
    static bool _consoleLogging = false;
    static bool _initialized = false;
    
    /// <summary>
    /// 
    /// </summary>
    public static void Initialize(bool createLogFile, bool outputToConsole = true)
    {
        _consoleLogging = outputToConsole;

        if (createLogFile)
        {
            Directory.CreateDirectory("./Logs");
            using var _ = File.Create(_activeLoggingFile = $"./Logs/{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
        }

        _initialized = true;

        Task.Run(ProcessQueue); // Background worker, this should never stop because of GetConsumingEnumerable().
    }
    
    static async Task ProcessQueue()
    {
        foreach (var entry in _queue.GetConsumingEnumerable())
        {
            if (_consoleLogging)
                Console.WriteLine(entry);

            if (!string.IsNullOrEmpty(_activeLoggingFile))
                await File.AppendAllTextAsync(_activeLoggingFile, "\n" + entry);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public static void Log(string log) { if (_initialized) _queue.Add("LOG: " + log); }
    
    /// <summary>
    /// 
    /// </summary>
    public static void Warning(string warning) { if (_initialized) _queue.Add("WRN: " + warning); }

    /// <summary>
    /// 
    /// </summary>
    public static void Error(string error, bool throwException = true)
    {
        if (!_initialized) return;
        _queue.Add("ERR: " + error);

        if (throwException)
            throw new Exception(error);
    }
}