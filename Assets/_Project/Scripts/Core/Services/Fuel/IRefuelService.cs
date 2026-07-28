public interface IRefuelService
{
    /// <summary>
    /// Возвращает текущее количество топлива
    /// активного корабля.
    /// </summary>
    int GetCurrentFuel();

    /// <summary>
    /// Возвращает вместимость топливного бака
    /// активного корабля.
    /// </summary>
    int GetFuelCapacity();

    /// <summary>
    /// Возвращает цену одной единицы топлива.
    /// </summary>
    int GetFuelUnitPrice();

    /// <summary>
    /// Проверяет возможность покупки топлива.
    /// State не изменяет.
    /// </summary>
    bool CanRefuel(int fuelCount);

    /// <summary>
    /// Покупает указанное количество топлива.
    /// </summary>
    RefuelResult Refuel(int fuelCount);

    /// <summary>
    /// Заправляет активный корабль до полного бака.
    /// </summary>
    RefuelResult RefuelToFull();

    /// <summary>
    /// Проверяет возможность расходования топлива.
    /// State не изменяет.
    /// </summary>
    bool CanConsume(
        int fuelCount,
        out FuelConsumeFailReason reason);

    /// <summary>
    /// Списывает топливо активного корабля.
    ///
    /// Метод не сохраняет игру самостоятельно.
    /// Владелец общей транзакции должен запросить
    /// сохранение после завершения всех изменений.
    /// </summary>
    bool Consume(
        int fuelCount,
        out FuelConsumeFailReason reason);
}