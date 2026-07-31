using UnityEngine;

/// <summary>
/// Визуальное состояние индикатора топлива.
///
/// Это presentation-состояние.
/// Оно не является новым источником gameplay-данных.
/// </summary>
public enum FuelHudVisualState2A
{
    Unavailable = 0,
    Normal = 1,
    Low = 2,
    Empty = 3
}