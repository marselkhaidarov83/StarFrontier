using UnityEngine;

/// <summary>
/// Изменение карты выходов по маршрутам в звёздной системе.
/// </summary>
public class RouteExitMapChangedEvent
{
    /// <summary>Конфигурация маршрута.</summary>
    public RouteConfig RouteConfig { get; }
    /// <summary>Идентификатор исходной системы.</summary>
    public string FromSystemId { get; }
    /// <summary>Идентификатор системы назначения.</summary>
    public string ToSystemId { get; }
    /// <summary>Точка выхода.</summary>
    public Vector3 ExitPoint { get; }
    /// <summary>Точка входа.</summary>
    public Vector3 EntryPoint { get; }
    /// <summary>Размер визуального эффекта или объекта.</summary>
    public float VisualSize { get; }

    public RouteExitMapChangedEvent(
        RouteConfig routeConfig,
        string fromSystemId,
        string toSystemId,
        Vector3 exitPoint,
        Vector3 entryPoint,
        float visualSize = 0f)
    {
        RouteConfig = routeConfig;
        FromSystemId = fromSystemId;
        ToSystemId = toSystemId;
        ExitPoint = exitPoint;
        EntryPoint = entryPoint;
        VisualSize = visualSize;
    }
}
