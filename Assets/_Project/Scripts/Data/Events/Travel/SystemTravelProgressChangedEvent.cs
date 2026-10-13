using UnityEngine;

/// <summary>
/// Изменение прогресса перемещения внутри системы: текущая позиция, цель и доля пути 0–1.
/// </summary>
public readonly struct SystemTravelProgressChangedEvent
{
    /// <summary>Текущая позиция объекта.</summary>
    public readonly Vector2 CurrentPosition;
    /// <summary>Позиция назначения.</summary>
    public readonly Vector2 DestinationPosition;
    /// <summary>Доля пройденного пути в диапазоне от 0 до 1.</summary>
    public readonly float Progress01;

    public SystemTravelProgressChangedEvent(
        Vector2 currentPosition,
        Vector2 destinationPosition,
        float progress01)
    {
        CurrentPosition = currentPosition;
        DestinationPosition = destinationPosition;
        Progress01 = progress01;
    }
}