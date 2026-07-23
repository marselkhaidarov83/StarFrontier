/// <summary>
/// Хранилище сервисов приложения.
///
/// Сервисы регистрируются под явно указанным типом:
/// обычно под публичным интерфейсом.
/// </summary>
public interface IServiceRegistry
{
    /// <summary>
    /// Количество зарегистрированных типов сервисов.
    /// Используется диагностикой и Debug HUD.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Регистрирует уже созданный экземпляр под типом TService.
    ///
    /// Один тип может быть зарегистрирован только один раз.
    /// Null регистрировать запрещено.
    /// </summary>
    void Register<TService>(TService service);

    /// <summary>
    /// Возвращает сервис, зарегистрированный под типом TService.
    ///
    /// Если сервис отсутствует, выбрасывает исключение.
    /// </summary>
    TService Get<TService>();

    /// <summary>
    /// Пытается получить сервис без выбрасывания исключения.
    /// </summary>
    bool TryGet<TService>(out TService service);
}
