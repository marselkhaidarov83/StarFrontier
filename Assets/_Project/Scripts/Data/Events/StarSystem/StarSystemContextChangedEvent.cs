/// <summary>
/// Смена контекста текущей звёздной системы.
/// </summary>
public readonly struct StarSystemContextChangedEvent
{
    /// <summary>Идентификатор звёздной системы.</summary>
    public readonly string SystemId;
    /// <summary>Уровень развития системы или инфраструктуры.</summary>
    public readonly int DevelopmentLevel;
    /// <summary>Уровень опасности объекта.</summary>
    public readonly int DangerLevel;
    /// <summary>Показатель стабильности системы.</summary>
    public readonly int Stability;
    /// <summary>Признак, что объект уже обнаружен.</summary>
    public readonly bool IsDiscovered;
    /// <summary>Признак, что система уже посещалась.</summary>
    public readonly bool IsVisited;

    public StarSystemContextChangedEvent(
        string systemId,
        int developmentLevel,
        int dangerLevel,
        int stability,
        bool isDiscovered,
        bool isVisited)
    {
        SystemId = systemId;
        DevelopmentLevel = developmentLevel;
        DangerLevel = dangerLevel;
        Stability = stability;
        IsDiscovered = isDiscovered;
        IsVisited = isVisited;
    }
}