/// <summary>
/// Уничтожение вражеского корабля; используется для очистки представления и HUD.
/// </summary>
public readonly struct SystemEnemyDestroyedEvent
{
    /// <summary>Идентификатор врага в runtime-состоянии.</summary>
    public readonly string RuntimeEnemyId;
    /// <summary>Идентификатор конфига врага.</summary>
    public readonly string EnemyConfigId;
    /// <summary>Идентификатор звёздной системы.</summary>
    public readonly string SystemId;
    /// <summary>Признак уничтожения цели игроком.</summary>
    public readonly bool KilledByPlayer;

    public SystemEnemyDestroyedEvent(
        string runtimeEnemyId,
        string enemyConfigId,
        string systemId,
        bool killedByPlayer)
    {
        RuntimeEnemyId = runtimeEnemyId;
        EnemyConfigId = enemyConfigId;
        SystemId = systemId;
        KilledByPlayer = killedByPlayer;
    }
}