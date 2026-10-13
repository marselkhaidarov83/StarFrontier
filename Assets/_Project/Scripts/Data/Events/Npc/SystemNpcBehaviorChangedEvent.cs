/// <summary>
/// Изменение режима поведения NPC; требуется обновить отображение/реакцию объекта.
/// </summary>
public readonly struct SystemNpcBehaviorChangedEvent
{
    /// <summary>Идентификатор NPC в текущем игровом состоянии.</summary>
    public readonly string RuntimeNpcId;
    /// <summary>Тип поведения NPC.</summary>
    public readonly SystemNpcBehaviorType BehaviorType;

    public SystemNpcBehaviorChangedEvent(
        string runtimeNpcId,
        SystemNpcBehaviorType behaviorType)
    {
        RuntimeNpcId = runtimeNpcId;
        BehaviorType = behaviorType;
    }
}