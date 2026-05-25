namespace Lutra.Core.Architecture
{
    /// <summary>
    /// Contrato que debe cumplir cada estado de la aplicación.
    /// </summary>
    public interface IAppState
    {
        /// <summary>Llamado al entrar en este estado.</summary>
        void OnEnter();

        /// <summary>Llamado al salir de este estado.</summary>
        void OnExit();

        /// <summary>Llamado cada frame mientras este estado está activo.</summary>
        void OnUpdate();
    }
}
