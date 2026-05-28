using System.Runtime.CompilerServices;
using UnityEngine;

public class CustomMonoBehaviour : MonoBehaviour
{
    [SerializeField] protected bool _debugEnabled;
    [SerializeField] protected bool _debugStop;

    protected bool IsDebug()
    {
        // return (_debugEnabled || Bootstrapper.Instance.GlobalDebugEnabled) && !_debugStop;
        return (_debugEnabled || Bootstrapper2A.Instance.GlobalDebugEnabled) && !_debugStop;
    }

    protected void LogCustom(string message, [CallerMemberName] string methodName = "")
    {
        if (IsDebug())
            Debug.Log($"{GetType().Name}.{methodName}: {message}");
    }    

    protected void LogCustomWarning(string message, [CallerMemberName] string methodName = "")
    {
        // if (IsDebug())
            Debug.LogWarning($"{GetType().Name}.{methodName}: {message}");
    }    
}