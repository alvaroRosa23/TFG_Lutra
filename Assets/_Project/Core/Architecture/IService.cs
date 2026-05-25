namespace Lutra.Core.Architecture
{
    /// <summary>
    /// Marca una clase como servicio registrable en el ServiceLocator.
    /// Los servicios MonoBehaviour deben heredar de <see cref="BaseService"/> en lugar
    /// de implementar esta interfaz directamente.
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// Registra esta instancia en el ServiceLocator usando el tipo concreto.
        /// La implementación por defecto no hace nada; <see cref="BaseService"/> la sobreescribe.
        /// </summary>
        void RegisterSelf() { }
    }
}
