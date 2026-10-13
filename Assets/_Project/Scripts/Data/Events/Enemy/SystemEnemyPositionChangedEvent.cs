using UnityEngine;

/// <summary>
/// Обновление позиции вражеского корабля.
/// </summary>
public readonly struct SystemEnemyPositionChangedEvent
{
    /// <summary>Идентификатор врага в runtime-состоянии.</summary>
    public readonly string RuntimeEnemyId;
    /// <summary>Позиция объекта в игровом пространстве.</summary>
    public readonly Vector3 Position;
    /// <summary>Направление, в котором ориентирован объект.</summary>
    public readonly Vector3 FacingDirection;

    public SystemEnemyPositionChangedEvent(
        string runtimeEnemyId,
        Vector3 position)
        : this(runtimeEnemyId, position, Vector3.up)
    {
    }

    public SystemEnemyPositionChangedEvent(
        string runtimeEnemyId,
        Vector3 position,
        Vector3 facingDirection)
    {
        RuntimeEnemyId = runtimeEnemyId;
        Position = position;
        FacingDirection = facingDirection;
    }
}