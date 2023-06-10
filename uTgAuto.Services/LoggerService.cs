using NLog;
using NLog.Config;
using NLog.Targets;

public static class LoggerService
{
    private static Logger? logger;

    static LoggerService()
    {
        ConfigureLogger();
    }

    private static void ConfigureLogger()
    {
        LogManager.Configuration = new XmlLoggingConfiguration("nlog.config");
        logger = LogManager.GetCurrentClassLogger();
    }

    public static void Debug(string message)
    {
        logger!.Debug(message);
    }

    public static void Info(string message)
    {
        logger!.Info(message);
    }

    public static void Error(string message)
    {
        logger!.Error(message);
    }

    public static void Warning(string message)
    {
        logger!.Warn(message);
        Console.ResetColor();
    }
}