/// <summary>
/// Контракт для обычного C#-объекта,
/// который должен обновляться один раз за кадр.
/// </summary>
public interface ITickable
{
    /// <summary>
    /// Выполняет обновление объекта.
    /// </summary>
    /// <param name="deltaTime">
    /// Время в секундах, прошедшее с предыдущего кадра.
    /// </param>
    void Tick(float deltaTime);
}
