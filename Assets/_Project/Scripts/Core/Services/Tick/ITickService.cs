/// <summary>
/// Управляет объектами, которые должны
/// обновляться через единый покадровый цикл.
/// </summary>
public interface ITickService : ITickable
{
    /// <summary>
    /// Количество зарегистрированных объектов.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Показывает, выполняется ли сейчас общий Tick.
    /// </summary>
    bool IsTicking { get; }

    /// <summary>
    /// Регистрирует обновляемый объект.
    /// Чем меньше order, тем раньше объект получает Tick.
    /// </summary>
    void Register(ITickable tickable, int order = 0);

    /// <summary>
    /// Удаляет объект из общего цикла.
    /// </summary>
    bool Unregister(ITickable tickable);

    /// <summary>
    /// Проверяет наличие объекта в общем цикле.
    /// </summary>
    bool Contains(ITickable tickable);

    /// <summary>
    /// Удаляет все зарегистрированные объекты.
    /// </summary>
    void Clear();
}