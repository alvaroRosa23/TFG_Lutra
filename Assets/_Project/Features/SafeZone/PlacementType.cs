namespace Lutra.Features.SafeZone
{
    /// <summary>
    /// Tipo de objeto decorativo. Determina en qué PlacementPoints puede colocarse.
    /// None = el item no es colocable (ej. accesorio de mascota).
    /// </summary>
    public enum PlacementType
    {
        None,
        Furniture,
        Plant,
        WallArt,
        Decoration
    }
}
