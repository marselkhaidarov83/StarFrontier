using System;
using System.Collections.Generic;

/// <summary>
/// Реестр готовых экземпляров сервисов.
///
/// Объект сохраняется под тем типом,
/// который был указан при вызове Register.
/// </summary>
public class ServiceRegistry : IServiceRegistry
{
    private readonly Dictionary<Type, object> _services = new();

    /// <summary>
    /// Регистрирует готовый экземпляр под типом TService.
    /// </summary>
    public void Register<TService>(TService service)
    {
        if (service is null)
        {
            throw new ArgumentNullException(
                nameof(service),
                $"Cannot register null as " +
                $"{typeof(TService).Name}.");
        }

        Type serviceType = typeof(TService);

        if (_services.ContainsKey(serviceType))
        {
            throw new InvalidOperationException(
                $"Service of type {serviceType.Name} " +
                "is already registered.");
        }

        _services.Add(serviceType, service);
    }

    /// <summary>
    /// Возвращает зарегистрированный сервис.
    /// </summary>
    public TService Get<TService>()
    {
        Type serviceType = typeof(TService);

        if (_services.TryGetValue(
                serviceType,
                out object service))
        {
            return (TService)service;
        }

        throw new InvalidOperationException(
            $"Service of type {serviceType.Name} " +
            "is not registered.");
    }

    /// <summary>
    /// Пытается вернуть зарегистрированный сервис.
    /// </summary>
    public bool TryGet<TService>(
        out TService service)
    {
        Type serviceType = typeof(TService);

        if (_services.TryGetValue(
                serviceType,
                out object foundService))
        {
            service = (TService)foundService;
            return true;
        }

        service = default;
        return false;
    }
}
