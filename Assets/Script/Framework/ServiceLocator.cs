using System;
using System.Collections.Generic;
using UnityEngine;

namespace PressureExpress.Framework
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> services = new Dictionary<Type, object>();
        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            if (service == null)
            {
                Debug.LogWarning($"[ServiceLocator] Attempted to register a null service for type '{type.Name}'.");
                return;
            }

            lock (services)
            {
                if (services.ContainsKey(type))
                {
                    services[type] = service;
                }
                else
                {
                    services.Add(type, service);
                }
            }
        }
        public static void Unregister<T>() where T : class
        {
            var type = typeof(T);
            lock (services)
            {
                if (services.ContainsKey(type))
                {
                    services.Remove(type);
                }
            }
        }
        public static void Unregister<T>(T service) where T : class
        {
            var type = typeof(T);
            lock (services)
            {
                if (services.TryGetValue(type, out var existing) && existing == service)
                {
                    services.Remove(type);
                }
            }
        }
        public static T Get<T>() where T : class
        {
            var type = typeof(T);
            lock (services)
            {
                if (services.TryGetValue(type, out var service))
                {
                    return (T)service;
                }
            }
            return null;
        }
        public static bool TryGet<T>(out T service) where T : class
        {
            service = Get<T>();
            return service != null;
        }
        public static void Clear()
        {
            lock (services)
            {
                services.Clear();
            }
        }
    }
}
