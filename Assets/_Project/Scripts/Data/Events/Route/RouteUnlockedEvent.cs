/// <summary>
/// Открытие нового маршрута между звёздными системами.
/// </summary>
public readonly struct RouteUnlockedEvent
{
    /// <summary>Идентификатор маршрута.</summary>
    public readonly string RouteId;

    public RouteUnlockedEvent(string routeId)
    {
        RouteId = routeId;
    }
}