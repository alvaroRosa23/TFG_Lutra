using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lutra.Core.Architecture
{
    /// <summary>
    /// Registro central de servicios. Permite obtener dependencias sin acoplamiento directo.
    /// Los servicios deben registrarse durante la inicialización (p.ej. Awake de GameBootstrapper)
    /// y eliminarse al destruirse el objeto propietario.
    /// </summary>
    public static class ServiceLocator
    {
        // Diccionario indexado por tipo de servicio
        private static readonly Dictionary<Type, object> _services = new();

        /// <summary>
        /// Registra una instancia de servicio. Si ya existía uno del mismo tipo, lo sobreescribe
        /// y emite un aviso.
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);

            if (_services.ContainsKey(type))
                Debug.LogWarning($"[ServiceLocator] Sobreescribiendo servicio ya registrado: {type.Name}");

            _services[type] = service;
        }

        /// <summary>
        /// Devuelve el servicio registrado del tipo solicitado.
        /// Lanza una excepción si no se ha registrado ninguno.
        /// </summary>
        public static T Get<T>() where T : class
        {
            var type = typeof(T);

            if (_services.TryGetValue(type, out var service))
                return (T)service;

            throw new InvalidOperationException(
                $"[ServiceLocator] Servicio no registrado: {type.Name}. " +
                "Asegúrate de registrarlo antes de usarlo.");
        }

        /// <summary>
        /// Elimina el registro del servicio indicado. No lanza error si no existía.
        /// </summary>
        public static void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        /// <summary>
        /// Registra un servicio usando su tipo en tiempo de ejecución (para BaseService.RegisterSelf).
        /// </summary>
        public static void RegisterByType(Type type, object service)
        {
            if (_services.ContainsKey(type))
                Debug.LogWarning($"[ServiceLocator] Sobreescribiendo servicio: {type.Name}");
            _services[type] = service;
        }

        /// <summary>
        /// Elimina el registro de un servicio por tipo en tiempo de ejecución.
        /// </summary>
        public static void UnregisterByType(Type type) => _services.Remove(type);

        /// <summary>
        /// Elimina todos los servicios registrados. Útil en tests o al reiniciar la app.
        /// </summary>
        public static void Clear()
        {
            _services.Clear();
        }
    }
}
