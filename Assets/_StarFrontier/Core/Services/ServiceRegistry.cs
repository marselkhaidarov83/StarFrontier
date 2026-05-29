using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarFrontier.Core.Services
{
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                Debug.LogError($"ServiceRegistry: cannot register null service {typeof(T).Name}");
                return;
            }

            _services[typeof(T)] = service;
        }

        public T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return service as T;
            }

            Debug.LogError($"ServiceRegistry: service {typeof(T).Name} not found.");
            return null;
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var rawService))
            {
                service = rawService as T;
                return service != null;
            }

            service = null;
            return false;
        }

        public void Clear()
        {
            _services.Clear();
        }
    }
}