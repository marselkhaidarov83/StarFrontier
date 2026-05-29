using System.Runtime.CompilerServices;
using UnityEngine;

public class CustomService
{
    [SerializeField] protected bool _debugEnabled;
    [SerializeField] protected bool _debugStop;

    protected bool IsDebug()
    {
        return (_debugEnabled || Bootstrapper.Instance.GlobalDebugEnabled) && !_debugStop;
    }

    protected void LogCustom(string message, [CallerMemberName] string methodName = "")
    {
        if (IsDebug())
            Debug.Log($"{GetType().Name}.{methodName}: {message}");
    }
}