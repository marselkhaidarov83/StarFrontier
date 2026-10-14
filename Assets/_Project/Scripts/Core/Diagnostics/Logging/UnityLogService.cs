using System;
using UnityEngine;

// UnityLogService — конечный адаптер между проектной системой логирования
// и Unity Console.
//
// Прямые вызовы Debug.* намеренно оставлены только в этом классе:
// весь игровой и редакторский код должен вызывать AppLog, AppLog передаёт
// сообщение сюда, а этот сервис перед выводом в UnityEngine.Debug проверяет
// разрешение канала через DebugLogConfig.
public sealed class UnityLogService : IAppLogService
{
    public void Info(DebugLogChannel channel, string message)
    {
        if (!IsEnabled(channel, message))
            return;

        Debug.Log("[DebugLog][" + channel + "] " + message);
    }

    public void Warning(DebugLogChannel channel, string message)
    {
        if (!IsEnabled(channel, message))
            return;

        Debug.LogWarning("[DebugLog][" + channel + "] " + message);
    }

    public void Error(DebugLogChannel channel, string message)
    {
        if (!IsEnabled(channel, message))
            return;

        Debug.LogError("[DebugLog][" + channel + "] " + message);
    }

    public void Exception(DebugLogChannel channel, Exception exception)
    {
        if (!IsEnabled(channel) ||
            exception == null)
        {
            return;
        }

        Debug.LogException(exception);
    }

    private static bool IsEnabled(DebugLogChannel channel, string message)
    {
        return !string.IsNullOrWhiteSpace(message) &&
               IsEnabled(channel);
    }

    private static bool IsEnabled(DebugLogChannel channel)
    {
        return Bootstrapper.Instance != null &&
               Bootstrapper.Instance.IsDebugLogEnabled(channel);
    }
}