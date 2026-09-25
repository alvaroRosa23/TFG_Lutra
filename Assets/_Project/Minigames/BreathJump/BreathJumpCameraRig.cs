using UnityEngine;
using UnityEngine.UI;

namespace Lutra.Minigames
{
    /// <summary>
    /// Cámara ortográfica de BreathJump: sigue a la nutria con suavizado y hace un zoom leve
    /// hacia ella mientras se inspira (y lo deshace al espirar).
    ///
    /// La escena principal usa un Canvas Screen Space - Overlay, que se dibuja encima de
    /// cualquier cámara. Para que el mundo del minijuego se vea, la cámara renderiza a una
    /// RenderTexture que se muestra en _output (RawImage a pantalla completa en el Canvas del
    /// minijuego). Si _output no está asignado, la cámara pinta directamente en pantalla.
    /// </summary>
    public class BreathJumpCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera   _camera;
        [SerializeField] private RawImage _output;

        [Header("Encuadre")]
        [SerializeField] private float   _baseOrthographicSize = 6f;
        [Tooltip("Desplazamiento de la cámara respecto a la nutria (mira hacia delante)")]
        [SerializeField] private Vector2 _lookAhead = new Vector2(2.5f, 1.5f);
        [SerializeField] private float   _followSmoothTime = 0.45f;

        [Header("Respiración")]
        [Tooltip("Zoom máximo al terminar de inspirar (0.12 = 12 % más cerca)")]
        [SerializeField, Range(0f, 0.3f)] private float _inhaleZoom = 0.12f;

        private RenderTexture _renderTexture;
        private Vector3 _followVelocity;

        private void Awake()
        {
            if (_camera == null) _camera = GetComponentInChildren<Camera>(true);
            if (_camera == null)
            {
                Debug.LogError("[BreathJumpCameraRig] Falta asignar la cámara");
                return;
            }

            _camera.orthographic = true;
            _camera.orthographicSize = _baseOrthographicSize;
            _setupRenderTexture();
        }

        private void OnDestroy()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }
        }

        /// <summary>Coloca la cámara sin suavizado (inicio de partida).</summary>
        public void SnapTo(Vector3 focus)
        {
            if (_camera == null) return;

            _followVelocity = Vector3.zero;
            _camera.transform.position = _framedPosition(focus, 0f);
            _camera.orthographicSize = _baseOrthographicSize;
        }

        /// <summary>
        /// breath01: 0 = pulmones vacíos, 1 = inspiración completa. El zoom acerca la cámara
        /// y reduce el "mirar hacia delante" para que el zoom vaya hacia la nutria.
        /// </summary>
        public void Tick(float deltaTime, Vector3 focus, float breath01)
        {
            if (_camera == null || deltaTime <= 0f) return;

            float eased = Mathf.SmoothStep(0f, 1f, breath01);
            _camera.orthographicSize = _baseOrthographicSize * (1f - _inhaleZoom * eased);

            _camera.transform.position = Vector3.SmoothDamp(_camera.transform.position,
                                                            _framedPosition(focus, eased),
                                                            ref _followVelocity,
                                                            _followSmoothTime,
                                                            Mathf.Infinity,
                                                            deltaTime);
        }

        // ── Helpers privados ───────────────────────────────────────────

        private Vector3 _framedPosition(Vector3 focus, float breath01)
        {
            Vector2 offset = _lookAhead * (1f - breath01 * 0.5f);
            return new Vector3(focus.x + offset.x, focus.y + offset.y, _camera.transform.position.z);
        }

        private void _setupRenderTexture()
        {
            if (_output == null) return;

            _renderTexture = new RenderTexture(Screen.width, Screen.height, 16)
            {
                name = "BreathJumpView"
            };
            _camera.targetTexture = _renderTexture;
            _output.texture = _renderTexture;

            // El fondo debe ser opaco: la cámara por defecto tiene alfa 0 y la RawImage
            // dejaría ver la interfaz de Main a través del cielo.
            _camera.clearFlags = CameraClearFlags.SolidColor;
            var background = _camera.backgroundColor;
            background.a = 1f;
            _camera.backgroundColor = background;
        }
    }
}
