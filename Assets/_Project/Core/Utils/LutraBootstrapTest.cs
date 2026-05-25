using System;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Persistence;

namespace Lutra.Core.Utils
{
    /// <summary>
    /// Test de arranque que verifica la inicialización de la base de datos
    /// y el acceso básico a datos. Adjuntar a cualquier GameObject en la escena
    /// de Bootstrap para ejecutarlo. Eliminar o deshabilitar en builds de producción.
    /// </summary>
    public class LutraBootstrapTest : MonoBehaviour
    {
        private async void Start()
        {
            try
            {
                // Esperar a que GameManager termine de inicializar la BD
                await Task.Delay(1000);

                var repo = ServiceLocator.Get<DataRepository>();

                // ── Test 1: lectura de UserProfile ─────────────────────────

                var profile = await repo.GetUserProfile();

                if (profile == null)
                    Debug.Log("✅ Primera ejecución detectada correctamente");
                else
                    Debug.Log($"✅ Perfil cargado: {profile.Name}");

                // ── Test 3: diagnóstico de fechas de EmotionRecord ─────────

                var debug = await repo.GetEmotionDatesDebug();
                Debug.Log($"[EmotionDebug]\n{debug}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LutraBootstrapTest] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
