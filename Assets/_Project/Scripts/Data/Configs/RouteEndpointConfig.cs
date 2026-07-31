using System;
using UnityEngine;

[CreateAssetMenu(fileName = "RouteEndpointConfig", menuName = "StarFrontier/Configs/Route end point")]
public class RouteEndpointConfig : BaseConfig
{
    [Header("Hyper Travel Points")]
    [SerializeField] private Vector3 exitPoint;
    [SerializeField] private Vector3 entryPoint;

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
}