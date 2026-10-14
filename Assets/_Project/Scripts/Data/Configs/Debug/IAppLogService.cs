using System;

public interface IAppLogService
{
    void Info(DebugLogChannel channel, string message);
    void Warning(DebugLogChannel channel, string message);
    void Error(DebugLogChannel channel, string message);
    void Exception(DebugLogChannel channel, Exception exception);
}