using System;
using UnityEngine;

// Конфиг RouteEndpointConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(fileName = "RouteEndpointConfig", menuName = "StarFrontier/Configs/Galaxy/Route end point")]
public class RouteEndpointConfig : BaseConfig
{
    [Header("Hyper Travel Points")]
    [Tooltip("Параметр exitPoint. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private Vector3 exitPoint;
    [Tooltip("Параметр entryPoint. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private Vector3 entryPoint;
    [Tooltip("Визуальный размер объекта на сцене.")]
    [SerializeField] [Min(0f)] private float visualSize = 50f;

    /// <summary>
    /// Точка выхода из этой системы в гиперпространство.
    /// Используется, когда игрок стартует из этой системы.
    /// </summary>
    public Vector3 ExitPoint => exitPoint;

    /// <summary>
    /// Точка входа из гиперпространства в эту систему.
    /// Используется, когда игрок прилетает в эту систему.
    /// </summary>
    public Vector3 EntryPoint => entryPoint;
    public float VisualSize => visualSize;
}
