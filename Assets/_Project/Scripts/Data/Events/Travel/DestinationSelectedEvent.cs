using UnityEngine;

/// <summary>
/// Выбор точки назначения внутри системы; используется при построении маршрута корабля.
/// </summary>
public readonly struct DestinationSelectedEvent
{
    /// <summary>Тип объекта назначения.</summary>
    public readonly TravelDestinationType DestinationType;
    /// <summary>Позиция назначения.</summary>
    public readonly Vector2 DestinationPosition;
    /// <summary>Идентификатор планеты.</summary>
    public readonly string PlanetId;
    /// <summary>Идентификатор станции.</summary>
    public readonly string StationId;
    /// <summary>Идентификатор целевой системы.</summary>
    public readonly string TargetSystemId;
    /// <summary>Идентификатор NPC в текущем игровом состоянии.</summary>
    public readonly string RuntimeNpcId;

    public DestinationSelectedEvent(
        TravelDestinationType destinationType,
        Vector2 destinationPosition,
        string planetId,
        string targetSystemId,
        string stationId = "",
        string runtimeNpcId = "")
    {
        DestinationType = destinationType;
        DestinationPosition = destinationPosition;
        PlanetId = planetId;
        StationId = stationId;
        TargetSystemId = targetSystemId;
        RuntimeNpcId = runtimeNpcId;
    }
}
