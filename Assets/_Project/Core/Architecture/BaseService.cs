using UnityEngine;

namespace Lutra.Core.Architecture
{
    /// <summary>
    /// Clase base para todos los servicios MonoBehaviour del proyecto.
    /// Implementa <see cref="IService.RegisterSelf"/> registrando la instancia
    /// concreta en el <see cref="ServiceLocator"/> usando el tipo en tiempo de ejecución,
    /// y la desregistra automáticamente en <see cref="OnDestroy"/>.
    ///
    /// Uso:
    ///   public class MyService : BaseService { ... }
    ///
    ///   Los subclases pueden sobreescribir OnDestroy para limpieza adicional,
    ///   pero deben llamar a base.OnDestroy() al final.
    /// </summary>
    public abstract class BaseService : MonoBehaviour, IService
    {
        /// <summary>
        /// Registra esta instancia en el ServiceLocator usando el tipo concreto.
        /// Llamado por GameManager durante el arranque.
        /// </summary>
        public virtual void RegisterSelf()
        {
            ServiceLocator.RegisterByType(GetType(), this);
        }

        /// <summary>
        /// Desregistra el servicio del ServiceLocator cuando se destruye el GameObject.
        /// Las subclases que sobreescriban este método deben llamar base.OnDestroy().
        /// </summary>
        protected virtual void OnDestroy()
        {
            ServiceLocator.UnregisterByType(GetType());
        }
    }
}
