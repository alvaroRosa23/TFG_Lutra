namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Indica el origen de un EmotionRecord.
    /// Permite distinguir registros reales del usuario de placeholders restaurados desde Firestore,
    /// sin mezclar metadatos técnicos con el campo Notes del usuario.
    /// </summary>
    public enum RecordSource
    {
        User                     = 0,
        RestoredFirestore        = 1,
        RestoredFirestoreHistory = 2,
    }
}
