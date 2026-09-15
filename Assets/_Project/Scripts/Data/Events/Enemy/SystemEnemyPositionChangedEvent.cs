using UnityEngine;

public readonly struct SystemEnemyPositionChangedEvent
{
    public readonly string RuntimeEnemyId;
    public readonly Vector3 Position;
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