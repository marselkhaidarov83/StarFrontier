/// <summary>
/// Факт уничтожения врага для потенциального учёта прогресса миссии; внешних ссылок в исходном аудите не найдено.
/// </summary>
public class EnemyDestroyedMissionEvent
{
    /// <summary>Идентификатор врага.</summary>
    public string EnemyId;
    /// <summary>Идентификатор звёздной системы.</summary>
    public string SystemId;
    /// <summary>Количество объектов, передаваемое событием.</summary>
    public int Count;
}