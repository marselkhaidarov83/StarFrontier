/// <summary>
/// Изменение списка/состояния выходов на карте системы; обновление визуализации карты.
/// </summary>
public sealed class ExitMapChangedEvent
{
    /// <summary>Маршрут выбранного выхода.</summary>
    public RouteConfig RouteConfig;
    public ExitMapChangedEvent(RouteConfig routeConfig)
    {
        RouteConfig = routeConfig;
    }
}
