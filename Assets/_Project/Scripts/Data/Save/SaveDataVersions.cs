/// <summary>
/// Версии формата save-state.
///
/// Не путать с GameRuntimeMetaState.SaveVersion:
/// SaveVersion — счётчик сохранений,
/// SaveDataVersion — версия структуры данных.
/// </summary>
public static class SaveDataVersions
{
    public const int Unknown = 0;

    /// <summary>
    /// Старое состояние до появления отдельной версии
    /// формата и направления корабля.
    /// </summary>
    public const int LegacyStage2A = 1;

    /// <summary>
    /// Версия, в которой добавлено направление корабля
    /// на системной карте.
    /// </summary>
    public const int ShipDirection = 2;

    public const int Current = ShipDirection;
}