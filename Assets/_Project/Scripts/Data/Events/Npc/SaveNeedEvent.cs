using System;

/// <summary>
/// Запрос/признак необходимости сохранения после изменения важного игрового состояния.
/// </summary>
public readonly struct SaveNeedEvent
{
    /// <summary>Причина завершения или поражения.</summary>
    public readonly string Reason;

    public SaveNeedEvent(string reason)
    {
        Reason = reason;
    }
}