using System;

/// <summary>
/// Контекст зависимостей HUD.
///
/// Контекст не создаёт сервисы.
/// Он только предоставляет доступ к уже готовым
/// экземплярам из IServiceRegistry.
/// </summary>
public sealed class SystemHudBindingContext2A
{
    public IServiceRegistry Services { get; }

    public int RegisteredServiceCount =>
        Services.Count;

    public SystemHudBindingContext2A(
        IServiceRegistry services)
    {
        Services = services ??
            throw new ArgumentNullException(
                nameof(services));
    }

    /// <summary>
    /// Получает обязательный сервис.
    /// Если сервис отсутствует, Registry выбросит
    /// диагностируемое исключение.
    /// </summary>
    public TService Get<TService>()
    {
        return Services.Get<TService>();
    }

    /// <summary>
    /// Пытается получить необязательный сервис.
    /// </summary>
    public bool TryGet<TService>(
        out TService service)
    {
        return Services.TryGet(out service);
    }
}