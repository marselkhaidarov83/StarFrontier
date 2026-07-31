/// <summary>
/// Тип выбранной цели в локальной сцене звёздной системы.
/// </summary>
public enum SystemGameplayTargetType
{
    None = 0,
    Planet = 10,
    Station = 20,
    TravelPoint = 30,
    Enemy = 40,
    Ally = 50,
    Resource = 60,
    Unknown = 999
}