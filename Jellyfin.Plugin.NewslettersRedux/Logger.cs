#pragma warning disable SA1611, CS0162
using System;
using System.IO;
using Jellyfin.Plugin.NewslettersRedux.Configuration;

namespace Jellyfin.Plugin.NewslettersRedux;

/// <summary>
/// Initializes a new instance of the <see cref="Logger"/> class.
/// </summary>
public class Logger
{
    private readonly PluginConfiguration config;
    private readonly string logFile;

    /// <summary>
    /// Initializes a new instance of the <see cref="Logger"/> class.
    /// </summary>
    public Logger()
    {
        config = Plugin.Instance!.Configuration;
        logFile = $"{config.LogDirectoryPath}/{GetDate()}_NewslettersRedux.log";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Debug"/> class.
    /// </summary>
    public void Debug(object msg)
    {
        if (config.DebugMode)
        {
            Inform(msg, "DEBUG");
        }
    }

    /// <summary>
    /// Inform info into the logs.
    /// </summary>
    public void Info(object msg)
    {
        Inform(msg, "INFO");
    }

    /// <summary>
    /// Inform warn into the logs.
    /// </summary>
    public void Warn(object msg)
    {
        Inform(msg, "WARN");
    }

    /// <summary>
    /// Inform error into the logs.
    /// </summary>
    public void Error(object msg)
    {
        Inform(msg, "ERR");
    }

    /// <summary>
    /// Inform specific type of warning into the logs.
    /// </summary>
    /// <param name="msg">The message to infrom into the logs.</param>
    /// <param name="type">Type of warning ("ERR", "WARN", "INFO").</param>
    private void Inform(object msg, string type)
    {
        var logMsgPrefix = $"[NLP]: {GetDateTime()} - [{type}] ";
        Console.WriteLine($"{logMsgPrefix}{msg}");
        File.AppendAllText(logFile, $"{logMsgPrefix}{msg}\n");
    }

    private static string GetDateTime()
    {
        return DateTime.Now.ToString("[yyyy-MM-dd] :: [HH:mm:ss]", System.Globalization.CultureInfo.CurrentCulture);
    }

    private static string GetDate()
    {
        return DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.CurrentCulture);
    }
}