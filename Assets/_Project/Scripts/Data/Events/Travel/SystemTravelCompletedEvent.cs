using UnityEngine;

/// <summary>
/// Завершение перемещения по маршруту внутри системы; сигнал прибытия.
/// </summary>
public readonly struct SystemTravelCompletedEvent
{
    /// <summary>Тип объекта назначения.</summary>
    public readonly TravelDestinationType DestinationType;
    /// <summary>Конечная позиция движения или эффекта.</summary>
    public readonly Vector3 FinalPosition;
    /// <summary>Идентификатор планеты.</summary>
    public readonly string PlanetId;
    /// <summary>Идентификатор целевой системы.</summary>
    public readonly string TargetSystemId;
    /// <summary>Конфигурация целевой звёздной системы.</summary>
    public readonly StarSystemConfig TargetSystemConfig;
    /// <summary>Ссылка на связанную систему.</summary>
    public readonly StarSystemLink SystemLink;

    public SystemTravelCompletedEvent(
        TravelDestinationType destinationType,
        Vector3 finalPosition,
        string planetId,
        string targetSystemId,
        StarSystemConfig targetSystemConfig,
        StarSystemLink systemLink)
    {
        DestinationType = destinationType;
        FinalPosition = finalPosition;
        PlanetId = planetId;
        TargetSystemId = targetSystemId;
        TargetSystemConfig = targetSystemConfig;
        SystemLink = systemLink;
    }
}