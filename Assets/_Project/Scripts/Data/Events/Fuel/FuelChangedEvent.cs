/// <summary>
/// Изменение количества топлива; обновление индикаторов и интерфейса заправки.
/// </summary>
public sealed class FuelChangedEvent
{
/// <summary>Текущее количество топлива.</summary>
public int CurrentFuel { get; }

        public FuelChangedEvent(int currentFuel)
        {
            CurrentFuel = currentFuel;
        }
}