using System;

public static class AppLog
{
    private static readonly IAppLogService NullService = new NullAppLogService();
    private static IAppLogService _service = NullService;

    public static IAppLogService Service => _service;

    public static void Configure(IAppLogService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public static void ResetToUnityLogger()
    {
        _service = NullService;
    }

    public static void Info(string message)
    {
        Info(DebugLogChannel.Bootstrap, message);
    }

    public static void Info(DebugLogChannel channel, string message)
    {
        _service.Info(channel, message);
    }

    public static void Info(DebugLogChannel channel, object source, string message)
    {
        Info(channel, FormatMessage(source, message));
    }

    public static void Warning(string message)
    {
        Warning(DebugLogChannel.Bootstrap, message);
    }

    public static void Warning(DebugLogChannel channel, string message)
    {
        _service.Warning(channel, message);
    }

    public static void Warning(DebugLogChannel channel, object source, string message)
    {
        Warning(channel, FormatMessage(source, message));
    }

    public static void Error(string message)
    {
        Error(DebugLogChannel.Bootstrap, message);
    }

    public static void Error(DebugLogChannel channel, string message)
    {
        _service.Error(channel, message);
    }

    public static void Error(DebugLogChannel channel, object source, string message)
    {
        Error(channel, FormatMessage(source, message));
    }

    public static void Exception(Exception exception)
    {
        Exception(DebugLogChannel.Bootstrap, exception);
    }

    public static void Exception(DebugLogChannel channel, Exception exception)
    {
        _service.Exception(channel, exception);
    }

    private static string FormatMessage(object source, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        return FormatSource(source) + message;
    }

    private static string FormatSource(object source)
    {
        if (source == null)
            return string.Empty;

        Type sourceType = source as Type ?? source.GetType();
        return "[" + sourceType.Name + "] ";
    }

    private sealed class NullAppLogService : IAppLogService
    {
        public void Info(DebugLogChannel channel, string message) { }

        public void Warning(DebugLogChannel channel, string message) { }

        public void Error(DebugLogChannel channel, string message) { }

        public void Exception(DebugLogChannel channel, Exception exception) { }
    }
}
