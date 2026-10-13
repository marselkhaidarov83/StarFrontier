using UnityEngine;

/// <summary>
/// Начало перемещения внутри звёздной системы.
/// </summary>
public readonly struct SystemTravelStartedEvent
{
    /// <summary>Тип объекта назначения.</summary>
    public readonly TravelDestinationType DestinationType;
    /// <summary>Начальная позиция воздействия или снаряда.</summary>
    public readonly Vector2 StartPosition;
    /// <summary>Позиция назначения.</summary>
    public readonly Vector2 DestinationPosition;

    public SystemTravelStartedEvent(
        TravelDestinationType destinationType,
        Vector2 startPosition,
        Vector2 destinationPosition)
    {
        DestinationType = destinationType;
        StartPosition = startPosition;
        DestinationPosition = destinationPosition;
    }
}
