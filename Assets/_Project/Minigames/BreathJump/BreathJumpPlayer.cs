using System;
using UnityEngine;

namespace Lutra.Minigames
{
    /// <summary>
    /// Nutria de BreathJump. Movimiento cinemático (sin Rigidbody) para que el salto sea
    /// exacto y predecible: con carga completa aterriza en el centro de la plataforma
    /// objetivo tras airTimeSeconds; con menos carga se queda corta en proporción.
    /// Mantener pulsado en el aire corrige la trayectoria hacia la plataforma y planea.
    ///
    /// El pivote del transform son los pies. Lo avanza BreathJumpController con Tick()
    /// (así la pausa lo congela). _visual recibe el squash de carga/aterrizaje y la
    /// inclinación en el aire; el Animator es opcional (parámetros "Charge" y "Airborne").
    /// </summary>
    public class BreathJumpPlayer : MonoBehaviour
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private Animator  _animator;

        [Header("Feedback visual")]
        [SerializeField] private float _chargeSquash     = 0.14f;
        [SerializeField] private float _landingSquash    = 0.18f;
        [SerializeField] private float _squashRecovery   = 6f;
        [SerializeField] private float _maxTiltDegrees   = 14f;
        [SerializeField] private float _tiltSmoothing    = 5f;

        private const float MinSteerTime = 0.4f;

        private enum State { Grounded, Airborne, Falling }

        /// <summary>Aterrizó en una plataforma (la objetivo, o de vuelta en la de origen si se quedó muy corta).</summary>
        public event Action<BreathJumpPlatform> OnLanded;

        /// <summary>Cayó por debajo del recorrido: hay que reaparecer.</summary>
        public event Action OnFell;

        private BreathJumpTuning   _tuning;
        private BreathJumpPlatform _origin;
        private BreathJumpPlatform _target;
        private State   _state = State.Grounded;
        private Vector2 _velocity;
        private bool    _steering;
        private float   _charge;
        private float   _landingImpulse;
        private float   _tilt;
        private Vector3 _visualBaseScale = Vector3.one;

        private int _chargeHash;
        private int _airborneHash;

        public bool IsGrounded => _state == State.Grounded;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_visual == null) _visual = transform;
            _visualBaseScale = _visual.localScale;

            _chargeHash   = Animator.StringToHash("Charge");
            _airborneHash = Animator.StringToHash("Airborne");
        }

        private void OnDestroy()
        {
            OnLanded = null;
            OnFell   = null;
        }

        // ── API pública ────────────────────────────────────────────────

        public void Setup(BreathJumpTuning tuning) => _tuning = tuning;

        /// <summary>Coloca la nutria de pie en el centro de una plataforma (inicio o reaparición).</summary>
        public void PlaceOn(BreathJumpPlatform platform)
        {
            if (platform == null) return;

            transform.position = new Vector3(platform.CenterX, platform.Top, transform.position.z);
            _origin   = platform;
            _target   = null;
            _velocity = Vector2.zero;
            _steering = false;
            _charge   = 0f;
            _tilt     = 0f;
            _landingImpulse = 0f;
            _setState(State.Grounded);
            _applyVisual();
        }

        /// <summary>Carga del salto (0-1) mientras se inspira: squash progresivo.</summary>
        public void SetCharge(float charge)
        {
            _charge = Mathf.Clamp01(charge);
            if (_animator != null) _animator.SetFloat(_chargeHash, _charge);
        }

        /// <summary>
        /// Salta hacia la plataforma objetivo. La velocidad vertical es siempre la del salto
        /// ideal; la horizontal se escala con la carga, así una inspiración corta se queda corta.
        /// </summary>
        public void Jump(BreathJumpPlatform target, float charge)
        {
            if (_state != State.Grounded || target == null || _tuning == null) return;

            float airTime = _tuning.airTimeSeconds;
            float dx = target.CenterX - transform.position.x;
            float dy = target.Top - transform.position.y;

            _velocity = new Vector2(dx * Mathf.Clamp01(charge) / airTime,
                                    dy / airTime + 0.5f * _tuning.Gravity * airTime);
            _target   = target;
            _steering = false;
            SetCharge(0f);
            _setState(State.Airborne);
        }

        /// <summary>Mantener pulsado en el aire: corrige hacia la plataforma y planea.</summary>
        public void SetSteering(bool steering) => _steering = steering && _state == State.Airborne;

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || _tuning == null) return;

            if (_state != State.Grounded) _tickAirborne(deltaTime);

            _landingImpulse = Mathf.MoveTowards(_landingImpulse, 0f, _squashRecovery * _landingSquash * deltaTime);
            float targetTilt = _state == State.Grounded
                ? 0f
                : Mathf.Clamp(_velocity.y * 6f, -_maxTiltDegrees, _maxTiltDegrees);
            _tilt = Mathf.Lerp(_tilt, targetTilt, 1f - Mathf.Exp(-_tiltSmoothing * deltaTime));

            _applyVisual();
        }

        // ── Helpers privados ───────────────────────────────────────────

        private void _tickAirborne(float deltaTime)
        {
            float gravity = _tuning.Gravity;
            if (_state == State.Falling)
                gravity *= _tuning.fallGravityMultiplier;
            else if (_steering && _velocity.y < 0f)
                gravity *= _tuning.glideGravityMultiplier;

            if (_steering && _state == State.Airborne && _target != null)
            {
                float timeLeft = _timeToReachHeight(_target.Top, gravity);
                float desiredVx = (_target.CenterX - transform.position.x) / timeLeft;
                _velocity.x = Mathf.MoveTowards(_velocity.x, desiredVx, _tuning.steerAcceleration * deltaTime);
            }

            _velocity.y -= gravity * deltaTime;

            Vector3 current = transform.position;
            Vector3 next = current + (Vector3)(_velocity * deltaTime);

            if (_velocity.y < 0f)
            {
                // Aterrizaje: solo desde arriba, al cruzar la superficie de la plataforma
                if (_state == State.Airborne && _crossesSurface(_target, current, next))
                {
                    _land(_target, next.x);
                    return;
                }

                // Salto demasiado corto: vuelve a caer sobre la plataforma de origen
                if (_crossesSurface(_origin, current, next))
                {
                    _land(_origin, next.x);
                    return;
                }

                if (_state == State.Airborne && _target != null && next.y < _target.Top)
                    _setState(State.Falling);
            }

            transform.position = next;

            float fallLimit = (_target != null ? _target.Top : current.y) - _tuning.fallDepthToRespawn;
            if (_state == State.Falling && next.y < fallLimit)
            {
                _velocity = Vector2.zero;
                OnFell?.Invoke();
            }
        }

        private bool _crossesSurface(BreathJumpPlatform platform, Vector3 current, Vector3 next)
        {
            if (platform == null) return false;
            if (current.y < platform.Top || next.y > platform.Top) return false;

            float grace = _tuning.landingEdgeGrace;
            return next.x >= platform.Left - grace && next.x <= platform.Right + grace;
        }

        private void _land(BreathJumpPlatform platform, float x)
        {
            float clampedX = Mathf.Clamp(x, platform.Left, platform.Right);
            transform.position = new Vector3(clampedX, platform.Top, transform.position.z);

            _origin   = platform;
            _target   = null;
            _velocity = Vector2.zero;
            _steering = false;
            _landingImpulse = _landingSquash;
            _setState(State.Grounded);

            OnLanded?.Invoke(platform);
        }

        /// <summary>Tiempo estimado hasta bajar a la altura indicada con la gravedad actual.</summary>
        private float _timeToReachHeight(float height, float gravity)
        {
            float above = transform.position.y - height;
            float discriminant = _velocity.y * _velocity.y + 2f * gravity * above;
            if (discriminant < 0f || gravity <= 0f) return MinSteerTime;
            return Mathf.Max((_velocity.y + Mathf.Sqrt(discriminant)) / gravity, MinSteerTime);
        }

        private void _setState(State state)
        {
            _state = state;
            if (_animator != null) _animator.SetBool(_airborneHash, state != State.Grounded);
        }

        private void _applyVisual()
        {
            float squash = _charge * _chargeSquash + _landingImpulse;
            _visual.localScale = Vector3.Scale(_visualBaseScale,
                                               new Vector3(1f + squash * 0.6f, 1f - squash, 1f));
            _visual.localRotation = Quaternion.Euler(0f, 0f, _tilt);
        }
    }
}
