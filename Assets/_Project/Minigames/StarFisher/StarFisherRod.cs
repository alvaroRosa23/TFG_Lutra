using UnityEngine;
using UnityEngine.UI;

namespace Lutra.Minigames
{
    /// <summary>
    /// Caña, sedal, anzuelo y estrella enganchada (solo visual). Todas las posiciones están en
    /// coordenadas locales de _space (el contenedor de la escena), así el anzuelo, las marcas y
    /// la zona segura pueden estar en cualquier sitio de la jerarquía.
    ///
    /// - La caña (_rod) gira sobre su pivote (colocarlo en la empuñadura): se echa hacia atrás al
    ///   cargar, vuelve al lanzar y se dobla con cada toque de la recogida.
    /// - El lanzamiento cae entre _castNear (fuerza mínima) y _castFar (máxima), siempre en la
    ///   misma dirección, con un arco.
    /// - Durante la espera el anzuelo sigue al dedo despacio, sin salir de _safeZone.
    /// </summary>
    public class StarFisherRod : MonoBehaviour
    {
        [Header("Espacio (contenedor de la escena)")]
        [SerializeField] private RectTransform _space;

        [Header("Caña")]
        [SerializeField] private RectTransform _rod;
        [SerializeField] private RectTransform _rodTip;
        [SerializeField] private float _restAngle       = 0f;
        [Tooltip("Grados que se echa hacia atrás con la barra de fuerza al máximo")]
        [SerializeField] private float _chargeBackAngle = 25f;
        [Tooltip("Grados que se dobla con cada toque de la recogida")]
        [SerializeField] private float _reelBendAngle   = 8f;
        [SerializeField] private float _angleSmoothing  = 14f;

        [Header("Anzuelo")]
        [SerializeField] private RectTransform  _hook;
        [SerializeField] private StarFisherLine _line;
        [SerializeField] private Image          _hookedStar;
        [SerializeField] private Image          _hookedGlow;
        [SerializeField] private float          _biteShakeAmount = 10f;

        [Header("Lanzamiento y zona segura")]
        [SerializeField] private RectTransform _castNear;
        [SerializeField] private RectTransform _castFar;
        [SerializeField] private RectTransform _safeZone;

        private enum HookMode { Hidden, Flying, Floating, Reeling }

        private HookMode _mode = HookMode.Hidden;
        private Vector2  _hookPosition;
        private Vector2  _flightStart, _flightEnd;
        private float    _flightTime, _flightDuration, _flightArc;
        private Vector2  _reelStart;
        private float    _reelProgress, _shownReelProgress;
        private float    _targetAngle, _bend;
        private bool     _biting;
        private float    _time;

        public Vector2 HookPosition => _hookPosition;
        public RectTransform Space  => _space;

        private void Awake()
        {
            if (_space == null) _space = (RectTransform)transform;
            if (_hookedStar != null) _hookedStar.raycastTarget = false;
            if (_hookedGlow != null) _hookedGlow.raycastTarget = false;
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Anzuelo recogido en la punta de la caña, sin estrella.</summary>
        public void ResetToRest()
        {
            _mode = HookMode.Hidden;
            _biting = false;
            _bend = 0f;
            _targetAngle = _restAngle;
            _hookPosition = _tipPosition();
            _applyHook(_hookPosition);
            ShowHookedStar(false);
            if (_line != null) _line.SetSlack(0.15f);
        }

        public void SetCharge(float power) => _targetAngle = _restAngle + _chargeBackAngle * Mathf.Clamp01(power);

        public void BeginCast(float power, float duration, float arcHeight)
        {
            _mode           = HookMode.Flying;
            _flightStart    = _tipPosition();
            _flightEnd      = _landingPoint(power);
            _flightTime     = 0f;
            _flightDuration = Mathf.Max(0.05f, duration);
            _flightArc      = arcHeight;
            _targetAngle    = _restAngle - _chargeBackAngle * 0.35f; // latigazo hacia delante
        }

        /// <summary>El anzuelo sigue a target (si hay dedo) a una velocidad máxima, dentro de la zona segura.</summary>
        public void Follow(Vector2? target, float maxSpeed, float deltaTime)
        {
            if (_mode != HookMode.Floating) return;

            if (target.HasValue)
                _hookPosition = Vector2.MoveTowards(_hookPosition, _clampToSafeZone(target.Value), maxSpeed * deltaTime);

            // La caña apunta un poco hacia donde está el anzuelo
            _targetAngle = _restAngle + Mathf.Clamp((_hookPosition.y - _flightEnd.y) * 0.02f, -8f, 8f);
        }

        public void SetBiting(bool biting) => _biting = biting;

        public void BeginReel()
        {
            _mode = HookMode.Reeling;
            _biting = false;
            _reelStart = _hookPosition;
            _reelProgress = _shownReelProgress = 0f;
        }

        /// <summary>Progreso visible de la recogida 0-1 (la estrella se acerca a la caña).</summary>
        public void SetReelProgress(float progress) => _reelProgress = Mathf.Clamp01(progress);

        /// <summary>Toque de recogida: la caña se dobla y recupera.</summary>
        public void PunchReel() => _bend = _reelBendAngle;

        public void ShowHookedStar(bool show, Sprite sprite = null, Color color = default, Color glowColor = default)
        {
            if (_hookedStar != null)
            {
                _hookedStar.enabled = show;
                if (show)
                {
                    _hookedStar.sprite = sprite;
                    _hookedStar.color  = color;
                    _hookedStar.preserveAspect = true;
                }
            }

            if (_hookedGlow != null)
            {
                _hookedGlow.enabled = show;
                if (show) _hookedGlow.color = glowColor;
            }
        }

        public void SetHookedGlowColor(Color color)
        {
            if (_hookedGlow != null) _hookedGlow.color = color;
        }

        /// <summary>Avanza las animaciones. Devuelve true el frame en que el anzuelo aterriza.</summary>
        public bool Tick(float deltaTime)
        {
            _time += deltaTime;
            bool landed = false;

            switch (_mode)
            {
                case HookMode.Hidden:
                    _hookPosition = _tipPosition();
                    break;

                case HookMode.Flying:
                    _flightTime += deltaTime;
                    float t = Mathf.Clamp01(_flightTime / _flightDuration);
                    _hookPosition = Vector2.Lerp(_flightStart, _flightEnd, 1f - (1f - t) * (1f - t))
                                  + Vector2.up * (_flightArc * 4f * t * (1f - t));
                    if (_line != null) _line.SetSlack(Mathf.Lerp(0.1f, 0.6f, t));
                    if (t >= 1f)
                    {
                        _mode = HookMode.Floating;
                        _targetAngle = _restAngle;
                        landed = true;
                    }
                    break;

                case HookMode.Floating:
                    if (_line != null) _line.SetSlack(_biting ? 0.1f : 0.55f);
                    break;

                case HookMode.Reeling:
                    _shownReelProgress = Mathf.Lerp(_shownReelProgress, _reelProgress, 1f - Mathf.Exp(-10f * deltaTime));
                    // La estrella se acerca hasta un poco antes de la punta
                    _hookPosition = Vector2.Lerp(_reelStart, _tipPosition(), _shownReelProgress * 0.8f);
                    if (_line != null) _line.SetSlack(0f);
                    break;
            }

            Vector2 shown = _hookPosition;
            if (_biting || _mode == HookMode.Reeling)
            {
                float amount = _biting ? _biteShakeAmount : _biteShakeAmount * 0.3f;
                shown += new Vector2(Mathf.Sin(_time * 53f), Mathf.Cos(_time * 47f)) * amount;
            }
            _applyHook(shown);

            // Caña: ángulo suavizado + dobleces de la recogida que se recuperan
            _bend = Mathf.Lerp(_bend, 0f, 1f - Mathf.Exp(-8f * deltaTime));
            if (_rod != null)
            {
                float current = _rod.localEulerAngles.z;
                float target  = _targetAngle - _bend;
                _rod.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.LerpAngle(current, target, 1f - Mathf.Exp(-_angleSmoothing * deltaTime)));
            }

            return landed;
        }

        public void SetLineVisible(bool visible)
        {
            if (_line != null) _line.SetVisible(visible);
        }

        // ── Helpers privados ───────────────────────────────────────────

        private Vector2 _tipPosition() => _rodTip != null ? _toSpace(_rodTip) : Vector2.zero;

        private Vector2 _landingPoint(float power)
        {
            Vector2 near = _castNear != null ? _toSpace(_castNear) : _tipPosition();
            Vector2 far  = _castFar  != null ? _toSpace(_castFar)  : near;
            return _clampToSafeZone(Vector2.Lerp(near, far, Mathf.Clamp01(power)));
        }

        private Vector2 _clampToSafeZone(Vector2 point)
        {
            if (_safeZone == null) return point;

            var corners = new Vector3[4];
            _safeZone.GetWorldCorners(corners);
            Vector2 min = _space.InverseTransformPoint(corners[0]);
            Vector2 max = _space.InverseTransformPoint(corners[2]);
            return new Vector2(Mathf.Clamp(point.x, Mathf.Min(min.x, max.x), Mathf.Max(min.x, max.x)),
                               Mathf.Clamp(point.y, Mathf.Min(min.y, max.y), Mathf.Max(min.y, max.y)));
        }

        private Vector2 _toSpace(RectTransform target) => _space.InverseTransformPoint(target.position);

        private void _applyHook(Vector2 spacePosition)
        {
            if (_hook != null) _hook.position = _space.TransformPoint(spacePosition);
        }
    }
}
