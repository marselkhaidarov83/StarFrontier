/// <summary>
/// Запрос межсистемного перелёта; внешних ссылок в исходном аудите не найдено.
/// </summary>
public readonly struct GalaxyTravelRequestedEvent
{
    /// <summary>Идентификатор исходной системы.</summary>
    public readonly string FromSystemId;
    /// <summary>Идентификатор системы назначения.</summary>
    public readonly string ToSystemId;
    /// <summary>Идентификатор маршрута.</summary>
    public readonly string RouteId;

    public GalaxyTravelRequestedEvent(
        string fromSystemId,
        string toSystemId,
        string routeId)
    {
        FromSystemId = fromSystemId;
        ToSystemId = toSystemId;
        RouteId = routeId;
    }
}