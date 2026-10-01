namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Tipos de elemento cortable del minijuego FruitNinja. La temática (qué se corta) está
    /// por decidir: los elementos son genéricos y su aspecto lo da SliceableDefinition.
    /// Los especiales desencadenan un efecto al cortarlos.
    /// </summary>
    public enum SliceableKind
    {
        Normal,
        Burst,       // al cortarlo sale un grupo de elementos juntos desde abajo
        Rain,        // durante unos segundos caen elementos desde arriba
        Crossfire,   // durante unos segundos salen elementos desde los laterales
        SlowMotion   // ralentiza el mundo unos segundos
    }
}
