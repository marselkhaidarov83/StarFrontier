using System;

public static class AppLog
{
    private static IAppLogService _service = new UnityLogService();

    public static IAppLogService Service => _service;

    public static void Configure(IAppLogService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public static void ResetToUnityLogger()
    {
        _service = new UnityLogService();
    }

    public static void Info(string message)
    {
        _service.Info(message);
    }

    public static void Warning(string message)
    {
        _service.Warning(message);
    }

    public static void Error(string message)
    {
        _service.Error(message);
    }

    public static void Exception(Exception exception)
    {
        _service.Exception(exception);
    }
}
