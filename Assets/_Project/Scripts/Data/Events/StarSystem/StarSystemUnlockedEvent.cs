/// <summary>
/// Открытие звёздной системы на карте галактики.
/// </summary>
public readonly struct StarSystemUnlockedEvent
{
    /// <summary>Идентификатор звёздной системы.</summary>
    public readonly string SystemId;

    public StarSystemUnlockedEvent(string systemId)
    {
        SystemId = systemId;
    }
}