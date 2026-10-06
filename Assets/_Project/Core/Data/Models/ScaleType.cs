namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Escala validada respondida por el usuario. Se persiste como int en ScaleResponse.Scale:
    /// no reordenar. Preparado para añadir otras escalas en el futuro.
    /// </summary>
    public enum ScaleType
    {
        Who5 = 0   // Índice de bienestar de la OMS (5 ítems, cada 14 días)
    }
}
