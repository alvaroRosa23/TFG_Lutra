using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Lutra.UI.Components
{
    /// <summary>
    /// Comparte archivos con el menú nativo del móvil (plugin NativeShare de yasirkula, licencia MIT,
    /// instalado por Package Manager; LutraCore define NATIVE_SHARE cuando el paquete está presente).
    /// En el editor abre la carpeta de los archivos para poder revisarlos.
    /// </summary>
    public static class FileSharer
    {
        public static void Share(IList<string> paths, string subject, string text)
        {
            if (paths == null || paths.Count == 0) return;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(paths[0]);
            Debug.Log($"[FileSharer] [EDITOR] Archivos generados en: {Path.GetDirectoryName(paths[0])}");
#elif NATIVE_SHARE
            var share = new NativeShare();
            foreach (string path in paths) share.AddFile(path);
            share.SetSubject(subject).SetText(text).Share();
#else
            Debug.LogWarning("[FileSharer] NativeShare no está instalado: no se puede abrir el menú de compartir.");
            ToastNotification.ShowInfo($"Archivos guardados en {Path.GetDirectoryName(paths[0])}");
#endif
        }
    }
}
