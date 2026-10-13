/// <summary>
/// Изменение режима следования камеры/игрока за NPC в системе.
/// </summary>
public readonly struct SystemNpcFollowModeChangedEvent2A
{
    /// <summary>Индекс выбранного режима.</summary>
    public readonly int ModeIndex;
    /// <summary>Номер выбранного режима.</summary>
    public readonly int ModeNumber;
    /// <summary>Дистанция следования за NPC.</summary>
    public readonly float FollowDistance;

    public SystemNpcFollowModeChangedEvent2A(
        int modeIndex,
        float followDistance)
    {
        ModeIndex = modeIndex;
        ModeNumber = modeIndex + 1;
        FollowDistance = followDistance;
    }
}