using NLog;
using NLog.Config;

public static class LoggerService
{
    private static Logger? logger;

    static LoggerService()
    {
        ConfigureLogger();
    }

    private static void ConfigureLogger()
    {
        LoggingConfiguration config = new XmlLoggingConfiguration("NLog.config");

#if DEBUG
        config.Variables["minLogLevel"] = "Trace";
#else
        config.Variables["minLogLevel"] = "Info";
#endif

        LogManager.Configuration = config;
        logger = LogManager.GetCurrentClassLogger();
    }

    public static void Trace(string message)
    {
        logger!.Trace(message);
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