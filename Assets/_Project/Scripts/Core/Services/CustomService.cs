using System.Runtime.CompilerServices;
using UnityEngine;

public class CustomService
{
    [SerializeField] protected bool _debugEnabled;
    [SerializeField] protected bool _debugStop;

    protected bool IsDebug()
    {
        return _debugEnabled && !_debugStop;
    }

    protected void LogCustom(string message, [CallerMemberName] string methodName = "")
    {
        if (!IsDebug())
            return;

        if (Bootstrapper.Instance == null)
            return;

        Bootstrapper.Instance.LogDebug(
            DebugLogChannel.Bootstrap,
            $"{GetType().Name}.{methodName}: {message}");
    }
}