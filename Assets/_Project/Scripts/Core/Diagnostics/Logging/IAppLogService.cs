using System;

public interface IAppLogService
{
    void Info(string message);
    void Warning(string message);
    void Error(string message);
    void Exception(Exception exception);
}
