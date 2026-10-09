using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lutra.UI.Components
{
    /// <summary>
    /// Pasa los Canvas raíz de una escena a Screen Space - Camera para que la UI se dibuje con URP y le
    /// llegue el filtro de daltonismo (ColorblindFeature). En Screen Space - Overlay la UI se dibuja
    /// después de URP y ningún Renderer Feature la ve.
    ///
    /// Se hace por código porque los Canvas de los minijuegos usan la cámara de Main.unity, que no se
    /// puede asignar desde el Inspector de otra escena. Una cámara propia de un minijuego (BreathJump)
    /// no se usa para la UI: pinta su mundo en una RenderTexture que la UI muestra.
    /// Lo llaman GameManager.Start (Main.unity; en Awake la escena aún no cuenta como cargada) y
    /// MinigameLoader (cada minijuego, después de cargar su escena).
    /// </summary>
    public static class CanvasCameraBinder
    {
        /// <summary>Distancia del plano de la UI a la cámara: por delante de cualquier sprite del mundo.</summary>
        private const float PlaneDistance = 1f;

        /// <summary>Configura los Canvas de una escena (Main.unity al arrancar).</summary>
        /// <param name="scene">Escena cuyos Canvas se configuran.</param>
        /// <param name="mainCamera">Cámara de Main.unity: la usan todos los Canvas, también los de los minijuegos.</param>
        public static void BindScene(Scene scene, Camera mainCamera)
            => _bind(scene, mainCamera, sortingFloor: null);

        /// <summary>
        /// Configura los Canvas de un minijuego para que queden por encima de toda la UI de
        /// <paramref name="belowScene"/> (Main.unity), como pasaba en Overlay: si su Canvas más bajo
        /// no supera al más alto de Main, todos suben lo mismo (se conserva su orden relativo).
        /// </summary>
        public static void BindSceneAbove(Scene scene, Camera mainCamera, Scene belowScene)
            => _bind(scene, mainCamera, sortingFloor: _highestSortingOrder(belowScene) + 1);

        private static void _bind(Scene scene, Camera mainCamera, int? sortingFloor)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                // Durante los Awake de una escena que se está cargando, isLoaded aún es false
                Debug.LogWarning($"[CanvasCameraBinder] La escena {scene.name} no está cargada: sus Canvas se quedan como estaban.");
                return;
            }

            // Siempre la cámara de Main.unity, también en minijuegos con cámara propia: la de BreathJump
            // pinta el mundo en una RenderTexture que se muestra en una RawImage de su Canvas; si el
            // Canvas se atara a esa cámara, tendría que dibujar su propia imagen y no se veía nada
            var canvases = _rootCanvases(scene);
            var camera   = mainCamera;
            if (camera == null)
            {
                Debug.LogWarning($"[CanvasCameraBinder] Sin cámara para los Canvas de {scene.name}: se quedan en Overlay.");
                return;
            }

            int shift = 0;
            if (sortingFloor.HasValue && canvases.Count > 0)
            {
                int lowest = int.MaxValue;
                foreach (var canvas in canvases) lowest = Mathf.Min(lowest, canvas.sortingOrder);
                shift = Mathf.Max(0, sortingFloor.Value - lowest);
            }

            foreach (var canvas in canvases)
            {
                canvas.renderMode    = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera   = camera;
                canvas.planeDistance = Mathf.Max(PlaneDistance, camera.nearClipPlane + 0.01f);
                canvas.sortingOrder += shift;

                // La cámara tiene que dibujar la capa del Canvas (en Overlay daba igual)
                camera.cullingMask |= 1 << canvas.gameObject.layer;

                Debug.Log($"[CanvasCameraBinder] {scene.name}/{canvas.name} → cámara {camera.name}, " +
                          $"orden {canvas.sortingOrder}, capa {LayerMask.LayerToName(canvas.gameObject.layer)}");
            }
        }

        /// <summary>Canvas raíz que no son World Space (los anidados heredan el modo del raíz).</summary>
        private static List<Canvas> _rootCanvases(Scene scene)
        {
            var result = new List<Canvas>();
            if (!scene.IsValid() || !scene.isLoaded) return result;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (_isRootCanvas(canvas) && canvas.renderMode != RenderMode.WorldSpace)
                        result.Add(canvas);
            return result;
        }

        private static int _highestSortingOrder(Scene scene)
        {
            int highest = 0;
            foreach (var canvas in _rootCanvases(scene))
                highest = Mathf.Max(highest, canvas.sortingOrder);
            return highest;
        }

        /// <summary>Canvas sin otro Canvas por encima (los anidados heredan el modo del raíz).</summary>
        private static bool _isRootCanvas(Canvas canvas)
        {
            var parent = canvas.transform.parent;
            return parent == null || parent.GetComponentInParent<Canvas>(true) == null;
        }
    }
}
