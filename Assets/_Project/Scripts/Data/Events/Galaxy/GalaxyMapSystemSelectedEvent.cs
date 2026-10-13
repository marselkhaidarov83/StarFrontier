using System.Collections.Generic;

/// <summary>
/// Выбор звёздной системы на карте галактики; используется информационной панелью.
/// </summary>
public class GalaxyMapSystemSelectedEvent
{
    /// <summary>Идентификатор целевой системы.</summary>
    public string TargetSystemId { get; }
    /// <summary>Идентификатор текущей звёздной системы.</summary>
    public string CurrentSystemId { get; }
    /// <summary>Путь перемещения или маршрут.</summary>
    public List<string> Path { get; }
    /// <summary>Идентификатор следующей системы.</summary>
    public string NextSystemId { get; }
    /// <summary>Причина неуспешного перелёта.</summary>
    public TravelFailReason TravelFailReason { get; }

    public GalaxyMapSystemSelectedEvent(
        string targetSystemId,
        string currentSystemId,
        List<string> path,
        string nextSystemId,
        TravelFailReason travelFailReason)
    {
        TargetSystemId = targetSystemId;
        CurrentSystemId = currentSystemId;
        Path = path != null
            ? new List<string>(path)
            : new List<string>();
        NextSystemId = nextSystemId;
        TravelFailReason = travelFailReason;
    }
}