using System;
using UnityEngine;

public sealed class UnityLogService : IAppLogService
{
    public void Info(string message)
    {
        Debug.Log(message);
    }

    public void Warning(string message)
    {
        Debug.LogWarning(message);
    }

    public void Error(string message)
    {
        Debug.LogError(message);
    }

    public void Exception(Exception exception)
    {
        if (exception == null)
            return;

        Debug.LogException(exception);
    }
}
