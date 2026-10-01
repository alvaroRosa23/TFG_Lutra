using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;
using Lutra.Core.Systems;
using Lutra.Features.StarCollection;

namespace Lutra.Minigames
{
    /// <summary>
    /// Minijuego StarFisher (Pescador de Estrellas): un astronauta sentado en la Luna pesca
    /// estrellas. 5 lanzamientos por partida; cada estrella pescada se registra en la colección y
    /// después el jugador la libera arrastrándola hacia arriba.
    ///
    /// Ciclo de un lanzamiento:
    ///   Ready → (mantener pulsado) Charging → (soltar) Casting → Waiting (5-10 s, el anzuelo se guía
    ///   con el dedo; las zonas brillantes suben la rareza) → Bite (2 s para tocar) → Reeling (toques
    ///   según la rareza; 1,5 s sin tocar = escapa) → Caught (destello blanco) → Reveal (ficha) →
    ///   Releasing (arrastrar hacia arriba) → siguiente lanzamiento.
    ///   Si escapa: Escaped (mensaje amable) → siguiente lanzamiento.
    ///
    /// Estrella de racha: el día en que la racha llega a streakStarDays, el primer lanzamiento
    /// pesca la estrella especial y no puede escaparse (ni al picar ni en la recogida).
    /// Sin catálogo asignado se usan estrellas de prueba (no se guardan en la colección).
    /// </summary>
    public class StarFisherController : MinigameBase
    {
        [SerializeField] private StarFisherView       _view;
        [SerializeField] private StarFisherRod        _rod;
        [SerializeField] private StarFisherInput      _input;
        [SerializeField] private StarFisherGlowZones  _glowZones;
        [SerializeField] private StarCollectionBookController _book;
        [SerializeField] private StarCatalog          _catalog;
        [SerializeField] private StarFisherTuning     _tuning = new StarFisherTuning();

        public override MinigameType Type => MinigameType.StarFisher;

        private bool _hasDependencies;

        private StarRoller        _roller;
        private StarFisherScoring _scoring;
        private StarCollectionStore    _collection;
        private List<StarDefinition> _regularStars = new List<StarDefinition>();
        private StarDefinition       _streakStar;
        private bool                 _usingPlaceholders;
        private readonly List<ScriptableObject> _runtimeAssets = new List<ScriptableObject>();

        // Estado del lanzamiento
        private StarFisherPhase _phase;
        private float    _phaseTime;
        private int      _castIndex;
        private float    _chargeTime;
        private float    _power;
        private bool     _perfectCast;
        private float    _waitLeft;
        private float    _glowTime;
        private Vector2? _followTarget;
        private float    _biteLeft;
        private int      _taps;
        private float    _idleTime;
        private int      _tier;
        private bool     _revealShown;

        private StarDefinition  _currentStar;
        private bool            _isStreakCatch;
        private bool            _streakStarPending;
        private StarCatchRecord _record;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null)      _view      = GetComponentInChildren<StarFisherView>(true);
            if (_rod == null)       _rod       = GetComponentInChildren<StarFisherRod>(true);
            if (_input == null)     _input     = GetComponentInChildren<StarFisherInput>(true);
            if (_glowZones == null) _glowZones = GetComponentInChildren<StarFisherGlowZones>(true);
            if (_book == null)      _book      = GetComponentInChildren<StarCollectionBookController>(true);

            _hasDependencies = _view != null && _rod != null && _input != null;
            if (!_hasDependencies)
                Debug.LogError("[StarFisherController] Falta asignar View, Rod o Input");
        }

        private void OnEnable()
        {
            if (!_hasDependencies) return;

            _input.OnPressed      += _onPressed;
            _input.OnDragged      += _onDragged;
            _input.OnReleased     += _onReleased;
            _view.OnReleaseClicked += _onReleaseClicked;
            _view.OnStarReleased   += _onStarReleased;
            _view.OnBookRequested  += _onBookRequested;
            _view.OnExitRequested  += _onExitRequested;
            _view.OnExitConfirmed  += _onExitConfirmed;
            _view.OnExitCancelled  += _onExitCancelled;
        }

        private void OnDisable()
        {
            if (!_hasDependencies) return;

            _input.OnPressed      -= _onPressed;
            _input.OnDragged      -= _onDragged;
            _input.OnReleased     -= _onReleased;
            _view.OnReleaseClicked -= _onReleaseClicked;
            _view.OnStarReleased   -= _onStarReleased;
            _view.OnBookRequested  -= _onBookRequested;
            _view.OnExitRequested  -= _onExitRequested;
            _view.OnExitConfirmed  -= _onExitConfirmed;
            _view.OnExitCancelled  -= _onExitCancelled;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var asset in _runtimeAssets)
                if (asset != null) Destroy(asset);
            _runtimeAssets.Clear();
        }

        private void Update()
        {
            if (!_isPlaying || !_hasDependencies || _scoring == null) return;

            float deltaTime = Time.deltaTime;
            _phaseTime += deltaTime;
            bool landed = _rod.Tick(deltaTime);

            switch (_phase)
            {
                case StarFisherPhase.Charging:  _tickCharging(deltaTime); break;
                case StarFisherPhase.Casting:   if (landed) _enterWaiting(); break;
                case StarFisherPhase.Waiting:   _tickWaiting(deltaTime); break;
                case StarFisherPhase.Bite:      _tickBite(deltaTime); break;
                case StarFisherPhase.Reeling:   _tickReeling(deltaTime); break;
                case StarFisherPhase.Caught:    _tickCaught(); break;
                case StarFisherPhase.Escaped:
                    if (_phaseTime >= _tuning.escapeMessageSeconds) _nextCast();
                    break;
                case StarFisherPhase.Finished:
                    if (_phaseTime >= _tuning.endMessageSeconds) EndGame(completedNaturally: true);
                    break;
            }

            if (_glowZones != null) _glowZones.Tick(deltaTime);
            _view.Tick(deltaTime);
        }

        // ── MinigameBase hooks ─────────────────────────────────────────

        protected override void OnInitialize()
        {
            if (!_hasDependencies) return;

            _roller  = new StarRoller(_tuning, Environment.TickCount);
            _scoring = new StarFisherScoring(_tuning);
            _prepareCatalog();
            _collection = new StarCollectionStore(_catalog);
            _streakStarPending = false;

            if (_glowZones != null)
            {
                _glowZones.Setup(_tuning, _roller);
                _glowZones.Clear();
            }

            _view.Initialize(_tuning);
            _castIndex = 0;
            _enterReady();

            if (!_usingPlaceholders) _ = _safeLoadCollection();
        }

        protected override void OnGamePaused()
        {
            _followTarget = null;

            // Si se pausa cargando la barra (p.ej. la app pasa a segundo plano) se vuelve a empezar
            if (_hasDependencies && _phase == StarFisherPhase.Charging) _enterReady();
        }

        protected override void OnGameEnded(bool completedNaturally)
        {
            if (_glowZones != null) _glowZones.Clear();

            // Un abandono no se guarda (MinigameLoader lo descarta): puntuación y monedas a 0.
            // Las estrellas ya pescadas sí se quedan en la colección.
            float score = completedNaturally && _scoring != null ? _scoring.CalculateRelaxationScore() : 0f;
            _result = BuildResult(score, completedNaturally, _scoring?.BuildMetrics());
            _result.CoinReward = completedNaturally && _scoring != null ? _scoring.CalculateCoinReward() : 0;
        }

        // ── Fases ──────────────────────────────────────────────────────

        private void _enterReady()
        {
            _setPhase(StarFisherPhase.Ready);
            _followTarget = null;
            _glowTime     = 0f;
            _perfectCast  = false;
            _power        = 0f;

            _rod.ResetToRest();
            _view.ShowPowerBar(false);
            _view.SetCasts(_castIndex + 1, _tuning.casts);
            _view.SetPose(AstronautPose.Cast);
            _view.SetHint("Mantén pulsado y suelta para lanzar");
            _view.SetBookButtonVisible(_book != null);
        }

        private void _enterCharging()
        {
            _setPhase(StarFisherPhase.Charging);
            _chargeTime = 0f;
            _view.SetBookButtonVisible(false);
            _view.ShowPowerBar(true);
            _view.SetHint("Suelta para lanzar");
        }

        private void _tickCharging(float deltaTime)
        {
            _chargeTime += deltaTime;
            float cycle = Mathf.Max(0.1f, _tuning.powerCycleSeconds);
            _power = Mathf.PingPong(_chargeTime * 2f / cycle, 1f);

            _view.SetPower(_power, _isPerfect(_power));
            _rod.SetCharge(_power);
        }

        private void _cast()
        {
            _perfectCast = _isPerfect(_power);
            _power = Mathf.Max(_tuning.minPower, _power);

            if (_perfectCast)
            {
                _scoring.RegisterPerfectCast();
                _view.ShowMessage("¡Lanzamiento perfecto!");
            }

            _setPhase(StarFisherPhase.Casting);
            _view.ShowPowerBar(false);
            _view.SetHint(string.Empty);
            _rod.BeginCast(_power, _tuning.castFlightSeconds, _tuning.castArcHeight);
        }

        private void _enterWaiting()
        {
            _setPhase(StarFisherPhase.Waiting);
            _waitLeft = _roller.Range(_tuning.waitSeconds.x, _tuning.waitSeconds.y);

            if (_glowZones != null) _glowZones.Begin();
            _view.SetPose(AstronautPose.Wait);
            _view.SetHint("Mueve el dedo para llevar el anzuelo a las zonas brillantes");
        }

        private void _tickWaiting(float deltaTime)
        {
            _rod.Follow(_followTarget, _tuning.hookFollowSpeed, deltaTime);

            if (_glowZones != null && _glowZones.Contains(_rod.HookPosition))
            {
                _glowTime += deltaTime;
                _scoring.AddGlowSeconds(deltaTime);
            }

            _waitLeft -= deltaTime;
            if (_waitLeft <= 0f) _enterBite();
        }

        /// <summary>Pica una estrella: aquí se decide cuál (el jugador no lo sabe hasta pescarla).</summary>
        private void _enterBite()
        {
            _isStreakCatch = _streakStarPending && _streakStar != null;
            if (_isStreakCatch)
            {
                _streakStarPending = false;
                _currentStar = _streakStar;
            }
            else
            {
                float glowFactor = _tuning.glowSecondsForMaxBonus > 0f ? _glowTime / _tuning.glowSecondsForMaxBonus : 0f;
                float bonus = _roller.CalculateBonus(_power, _perfectCast, glowFactor);
                var rarity  = _roller.RollRarity(bonus);
                _currentStar = _roller.PickStar(rarity, _regularStars, _collection.IsDiscovered);
            }

            _setPhase(StarFisherPhase.Bite);
            _biteLeft = _tuning.biteWindowSeconds;
            _followTarget = null;

            if (_glowZones != null) _glowZones.Stop();
            _rod.SetBiting(true);
            _rod.ShowHookedStar(true, StarSpriteFactory.Star, Color.white, _tierColor(0));
            _view.ShowBite(true);
            _view.SetBiteTimeLeft(_isStreakCatch ? -1f : 1f);
            _view.SetHint("¡Ha picado! Toca la pantalla");

#if UNITY_ANDROID || UNITY_IOS
            if (_tuning.vibrateOnBite) Handheld.Vibrate();
#endif
        }

        private void _tickBite(float deltaTime)
        {
            if (_isStreakCatch) return; // la estrella de racha espera lo que haga falta

            _biteLeft -= deltaTime;
            _view.SetBiteTimeLeft(_biteLeft / Mathf.Max(0.01f, _tuning.biteWindowSeconds));
            if (_biteLeft <= 0f) _escape(atBite: true);
        }

        private void _enterReeling()
        {
            _setPhase(StarFisherPhase.Reeling);
            _taps     = 0;
            _idleTime = 0f;
            _tier     = 0;

            _rod.BeginReel();
            _view.ShowBite(false);
            _view.SetPose(AstronautPose.Reel);
            _view.SetHint("¡Toca rápido para tirar de ella!");
            _view.SetReelIdle(_isStreakCatch ? -1f : 1f);
            _view.SetReelTier(_zoomForTier(0), _tierColor(0), _hookWorldPosition(), pulse: true);
        }

        private void _tickReeling(float deltaTime)
        {
            // El zoom sigue a la estrella mientras se acerca
            _view.SetReelTier(_zoomForTier(_tier), _tierColor(_tier), _hookWorldPosition(), pulse: false);

            if (_isStreakCatch) return;

            _idleTime += deltaTime;
            _view.SetReelIdle(1f - _idleTime / Mathf.Max(0.01f, _tuning.reelIdleEscapeSeconds));
            if (_idleTime >= _tuning.reelIdleEscapeSeconds) _escape(atBite: false);
        }

        private void _registerTap()
        {
            _taps++;
            _idleTime = 0f;
            _rod.PunchReel();

            int rarity   = (int)_currentStar.rarity;
            int required = _tuning.GetTaps(rarity);
            _rod.SetReelProgress((float)_taps / Mathf.Max(1, required));

            if (_taps >= required)
            {
                _enterCaught();
                return;
            }

            // Cada umbral superado de una rareza inferior revela que es, al menos, la siguiente
            bool passed = false;
            while (_tier < rarity && _taps >= _tuning.GetTaps(_tier))
            {
                _tier++;
                passed = true;
            }

            if (!passed) return;
            _rod.SetHookedGlowColor(_tierColor(_tier));
            _view.SetReelTier(_zoomForTier(_tier), _tierColor(_tier), _hookWorldPosition(), pulse: true);
        }

        private void _enterCaught()
        {
            _setPhase(StarFisherPhase.Caught);
            _revealShown = false;

            _record = _usingPlaceholders
                ? new StarCatchRecord { IsNew = true, TimesCaught = 1 }
                : _collection.RegisterCatch(_currentStar);
            _scoring.RegisterCatch(_currentStar.rarity, _record.IsNew, _isStreakCatch);

            _view.SetReelIdle(-1f);
            _view.SetHint(string.Empty);
            _view.SetBookButtonVisible(false);
        }

        /// <summary>Destello blanco: sube, se mantiene (debajo se prepara la ficha) y baja.</summary>
        private void _tickCaught()
        {
            float fadeIn = Mathf.Max(0.01f, _tuning.flashInSeconds);
            float hold   = _tuning.flashHoldSeconds;
            float fadeOut = Mathf.Max(0.01f, _tuning.flashOutSeconds);
            float t = _phaseTime;

            if (t < fadeIn)
            {
                _view.SetWhiteFlash(t / fadeIn);
                return;
            }

            if (!_revealShown) _showReveal();

            if (t < fadeIn + hold)
            {
                _view.SetWhiteFlash(1f);
                return;
            }

            float k = (t - fadeIn - hold) / fadeOut;
            _view.SetWhiteFlash(1f - k);
            if (k >= 1f)
            {
                _view.SetWhiteFlash(0f);
                _setPhase(StarFisherPhase.Reveal);
            }
        }

        private void _showReveal()
        {
            _revealShown = true;

            _rod.ResetToRest();
            _view.ResetReelEffects(immediate: true);
            _view.SetPose(AstronautPose.Wait);
            _view.ShowReveal(new StarRevealData
            {
                Star        = _currentStar,
                RarityColor = _tierColor((int)_currentStar.rarity),
                IsNew       = _record.IsNew,
                TimesCaught = _record.TimesCaught
            });

            if (_record.CompletedCollection)
                _view.ShowMessage("¡Colección completa! Has conseguido el telescopio para tu zona segura");
        }

        private void _escape(bool atBite)
        {
            _scoring.RegisterEscape(atBite);
            _setPhase(StarFisherPhase.Escaped);

            _rod.ResetToRest();
            if (_glowZones != null) _glowZones.Stop();
            _view.ShowBite(false);
            _view.SetReelIdle(-1f);
            _view.ResetReelEffects();
            _view.SetPose(AstronautPose.Wait);
            _view.SetHint(string.Empty);
            _view.ShowMessage(atBite
                ? "La estrella se ha ido... No pasa nada, habrá más."
                : "Se ha escapado... No pasa nada, habrá más.");
        }

        private void _nextCast()
        {
            _castIndex++;
            if (_castIndex < _tuning.casts)
            {
                _enterReady();
                return;
            }

            _setPhase(StarFisherPhase.Finished);
            _view.SetHint(string.Empty);
            _view.SetBookButtonVisible(false);
            _view.ShowMessage(_scoring.Caught == _tuning.casts
                ? "¡Pesca perfecta! Has pescado todas las estrellas"
                : $"¡Buena pesca! {_scoring.Caught} de {_tuning.casts} estrellas");
        }

        // ── Handlers de entrada ────────────────────────────────────────

        private void _onPressed(Vector2 position)
        {
            if (!_isPlaying) return;

            switch (_phase)
            {
                case StarFisherPhase.Ready:   _enterCharging(); break;
                case StarFisherPhase.Waiting: _followTarget = position; break;
                case StarFisherPhase.Bite:    _enterReeling(); break;
                case StarFisherPhase.Reeling: _registerTap(); break;
            }
        }

        private void _onDragged(Vector2 position)
        {
            if (_isPlaying && _phase == StarFisherPhase.Waiting) _followTarget = position;
        }

        private void _onReleased()
        {
            _followTarget = null;
            if (_isPlaying && _phase == StarFisherPhase.Charging) _cast();
        }

        // ── Handlers de la vista ───────────────────────────────────────

        private void _onReleaseClicked()
        {
            if (!_isPlaying || _phase != StarFisherPhase.Reveal) return;

            _setPhase(StarFisherPhase.Releasing);
            _view.BeginRelease(_currentStar);
        }

        private void _onStarReleased()
        {
            if (_phase != StarFisherPhase.Releasing) return;

            _view.EndRelease();
            _nextCast();
        }

        private void _onBookRequested()
        {
            if (!_isPlaying || _phase != StarFisherPhase.Ready || _book == null) return;

            PauseGame();
            _book.Open(_usingPlaceholders ? null : _collection, ResumeGame);
        }

        private void _onExitRequested()
        {
            if (!_isPlaying) return;

            PauseGame();
            _view.ShowExitConfirm(true);
        }

        // Salir antes de terminar es un abandono: no se guarda la partida (las estrellas sí).
        private void _onExitConfirmed()
        {
            _view.ShowExitConfirm(false);
            EndGame(completedNaturally: false);
        }

        private void _onExitCancelled()
        {
            _view.ShowExitConfirm(false);
            ResumeGame();
        }

        // ── Carga de datos ─────────────────────────────────────────────

        /// <summary>Carga la colección y comprueba si hoy toca la estrella de racha.</summary>
        private async Task _safeLoadCollection()
        {
            try
            {
                await _collection.LoadAsync();

                if (_streakStar == null) return;

                var streakManager = ServiceLocator.Get<StreakManager>();
                int  streak       = await streakManager.GetCurrentStreak();
                bool today        = await streakManager.HasCheckedInToday();

                _streakStarPending = today
                                  && streak == _tuning.streakStarDays
                                  && !_collection.WasCaughtToday(_streakStar.starId);

                if (_streakStarPending)
                    Debug.Log("[StarFisherController] Hoy toca la estrella de racha en el primer lanzamiento");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StarFisherController] No se pudo cargar la colección: {ex.Message}");
            }
        }

        /// <summary>Estrellas del sorteo; sin catálogo (o vacío) crea una de prueba por rareza.</summary>
        private void _prepareCatalog()
        {
            if (_catalog == null)
            {
                _catalog = ScriptableObject.CreateInstance<StarCatalog>();
                _runtimeAssets.Add(_catalog);
            }

            _regularStars = _catalog.GetRegularStars();
            _streakStar   = _catalog.GetStreakStar();
            _usingPlaceholders = _regularStars.Count == 0;
            if (!_usingPlaceholders) return;

            Debug.LogWarning("[StarFisherController] Sin estrellas en el catálogo: se usan estrellas de prueba");
            for (int r = 0; r < StarRarityExtensions.Count; r++)
            {
                var rarity = (StarRarity)r;
                var star = ScriptableObject.CreateInstance<StarDefinition>();
                star.starId      = $"placeholder_{r}";
                star.displayName = $"Estrella de prueba ({rarity.ToDisplayName()})";
                star.rarity      = rarity;
                star.tint        = _catalog.GetRarityColor(rarity);
                star.weight      = "?";
                star.age         = "?";
                star.description = "Estrella de prueba: asigna un StarCatalog al StarFisherController.";
                star.phrase      = "Cada paso cuenta, aunque sea pequeño.";
                _regularStars.Add(star);
                _runtimeAssets.Add(star);
            }
        }

        // ── Helpers privados ───────────────────────────────────────────

        private void _setPhase(StarFisherPhase phase)
        {
            _phase     = phase;
            _phaseTime = 0f;
        }

        private bool _isPerfect(float power) => power >= _tuning.perfectZone.x && power <= _tuning.perfectZone.y;

        private Color _tierColor(int rarity) => _catalog.GetRarityColor((StarRarity)Mathf.Clamp(rarity, 0, StarRarityExtensions.Count - 1));

        private float _zoomForTier(int tier) => 1f + _tuning.zoomPerTier * (tier + 1);

        private Vector3 _hookWorldPosition() => _rod.Space.TransformPoint(_rod.HookPosition);
    }
}
