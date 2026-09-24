using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.ScriptableObjects;

namespace Lutra.Minigames
{
    /// <summary>
    /// Vista pura de Beatmaker: no contiene lógica de negocio, solo refleja el estado
    /// que le indica BeatmakerController y expone eventos de interacción del usuario.
    ///
    /// El secuenciador se genera en Awake: una BeatmakerTrackRow (prefab) por pista dentro
    /// de _tracksContainer. La línea de reproducción (_playhead) debe estar FUERA de ese
    /// contenedor (se limpia al generar las filas) y cubrir verticalmente la rejilla; solo
    /// se modifica su X. Cada botón de loop puede tener su propia línea hija (_loopPlayheads).
    /// </summary>
    public class BeatmakerView : MonoBehaviour
    {
        [Header("Secuenciador (8 pistas x 8 steps)")]
        [SerializeField] private BeatmakerTrackRow _trackRowPrefab;
        [SerializeField] private RectTransform     _tracksContainer;
        [SerializeField] private RectTransform     _playhead;

        [Header("Loops instrumentales (exclusivos)")]
        [SerializeField] private Button[]          _loopButtons   = new Button[2];
        [SerializeField] private TextMeshProUGUI[] _loopLabels    = new TextMeshProUGUI[2];
        [SerializeField] private RectTransform[]   _loopPlayheads = new RectTransform[2];

        [Header("Metrónomo")]
        [SerializeField] private Button          _metronomeButton;
        [SerializeField] private TextMeshProUGUI _metronomeLabel;

        [Header("Pack y tempo")]
        [SerializeField] private TMP_Dropdown    _packDropdown;
        [SerializeField] private TextMeshProUGUI _bpmLabel;
        [SerializeField] private TextMeshProUGUI _scoreLabel;

        [Header("Colores de estado")]
        [SerializeField] private Color _padActiveColor   = new Color(1f, 0.85f, 0.4f, 1f);
        [SerializeField] private Color _padInactiveColor = Color.white;

        [Header("Salida")]
        [SerializeField] private Button _exitButton;

        // ── Eventos ────────────────────────────────────────────────────

        /// <summary>(pista 0-7, step 0-7)</summary>
        public event Action<int, int> OnStepToggled;
        public event Action<int> OnLoopToggled;
        public event Action<int> OnPackSelected;
        public event Action OnMetronomeToggled;
        public event Action OnExitRequested;

        private readonly List<BeatmakerTrackRow> _rows = new List<BeatmakerTrackRow>();
        private readonly Vector3[] _corners = new Vector3[4];
        private int _lastScore = -1;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _buildTrackRows();
            _wireLoops();

            if (_metronomeButton != null)
                _metronomeButton.onClick.AddListener(() => OnMetronomeToggled?.Invoke());

            if (_packDropdown != null)
                _packDropdown.onValueChanged.AddListener(index => OnPackSelected?.Invoke(index));

            if (_exitButton != null)
                _exitButton.onClick.AddListener(() => OnExitRequested?.Invoke());
        }

        private void OnDestroy()
        {
            OnStepToggled      = null;
            OnLoopToggled      = null;
            OnPackSelected     = null;
            OnMetronomeToggled = null;
            OnExitRequested    = null;
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Prepara la vista para una nueva partida: lista de packs, pack activo y todo apagado.</summary>
        public void Initialize(IReadOnlyList<BeatmakerSoundPack> packs, int activePackIndex)
        {
            _fillPackDropdown(packs, activePackIndex);
            ApplyPack(activePackIndex >= 0 && packs != null && activePackIndex < packs.Count
                ? packs[activePackIndex]
                : null);

            _resetVisualState();
        }

        /// <summary>Refleja el pack activo: BPM y nombres de loop.</summary>
        public void ApplyPack(BeatmakerSoundPack pack)
        {
            if (_bpmLabel != null)
                _bpmLabel.text = pack != null ? $"{pack.bpm} BPM" : string.Empty;

            _setLoopLabels(pack != null ? pack.loopNames : null);
        }

        public void SetStepState(int track, int step, bool active)
        {
            if (track < 0 || track >= _rows.Count || _rows[track] == null) return;
            _rows[track].SetStepColor(step, active ? _padActiveColor : _padInactiveColor);
        }

        public void SetLoopState(int loopIndex, bool active)
        {
            if (_loopButtons == null || loopIndex < 0 || loopIndex >= _loopButtons.Length) return;
            _setButtonColor(_loopButtons[loopIndex], active ? _padActiveColor : _padInactiveColor);
        }

        public void SetMetronomeState(bool enabled)
        {
            _setButtonColor(_metronomeButton, enabled ? _padActiveColor : _padInactiveColor);
            if (_metronomeLabel != null)
                _metronomeLabel.text = enabled ? "Metrónomo: ON" : "Metrónomo: OFF";
        }

        /// <summary>Puntuación 0-100; solo reescribe el texto si cambia.</summary>
        public void SetScore(int score)
        {
            if (_scoreLabel == null || score == _lastScore) return;
            _lastScore = score;
            _scoreLabel.text = $"Puntuación: {score}";
        }

        /// <summary>
        /// Mueve la línea del secuenciador (posición 0-1 en el ciclo de 8 steps) y la del loop
        /// activo (posición 0-1 dentro del loop, que puede durar varias vueltas del patrón).
        /// </summary>
        public void UpdatePlayheads(bool running, float cycleProgress, int activeLoop, float loopProgress)
        {
            bool showGrid = running && _rows.Count > 0 && _rows[0] != null;
            _setActive(_playhead, showGrid);
            if (showGrid) _positionGridPlayhead(cycleProgress);

            if (_loopPlayheads == null) return;

            for (int i = 0; i < _loopPlayheads.Length; i++)
            {
                var line = _loopPlayheads[i];
                if (line == null) continue;

                bool show = running && i == activeLoop;
                _setActive(line, show);
                if (!show || _loopButtons == null || i >= _loopButtons.Length || _loopButtons[i] == null) continue;

                var buttonRect = (RectTransform)_loopButtons[i].transform;
                buttonRect.GetWorldCorners(_corners);
                _setWorldX(line, Mathf.Lerp(_corners[0].x, _corners[2].x, loopProgress));
            }
        }

        // ── Helpers privados ───────────────────────────────────────────

        private void _buildTrackRows()
        {
            _clearContainer(_tracksContainer);
            _rows.Clear();

            if (_trackRowPrefab == null || _tracksContainer == null)
            {
                Debug.LogError("[BeatmakerView] Falta asignar _trackRowPrefab o _tracksContainer");
                return;
            }

            for (int track = 0; track < BeatmakerPatternState.TrackCount; track++)
            {
                var row = Instantiate(_trackRowPrefab, _tracksContainer, false);
                row.name = $"TrackRow_{track}";
                row.SetLabel(_trackName(track));

                int trackIndex = track; // captura local
                row.OnStepClicked += step => OnStepToggled?.Invoke(trackIndex, step);
                _rows.Add(row);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_tracksContainer);
            if (_tracksContainer.parent is RectTransform parentRt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
        }

        private void _clearContainer(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                child.gameObject.SetActive(false);  // síncrono; LayoutGroup ignora inactivos inmediatamente
                Destroy(child.gameObject);
            }
        }

        private static string _trackName(int track)
        {
            string instrument = BeatmakerPatternState.InstrumentOf(track).ToString().ToUpperInvariant();
            return $"{instrument} {BeatmakerPatternState.VariationOf(track) + 1}";
        }

        /// <summary>
        /// La línea cruza el borde izquierdo de cada step justo cuando ese step suena y se
        /// desliza hasta el siguiente (incluido el hueco entre los dos grupos de 4).
        /// </summary>
        private void _positionGridPlayhead(float cycleProgress)
        {
            var row = _rows[0];
            int steps = Mathf.Min(row.StepCount, BeatmakerPatternState.StepCount);
            if (steps == 0) return;

            float stepPosition = cycleProgress * steps;
            int step = Mathf.Clamp(Mathf.FloorToInt(stepPosition), 0, steps - 1);
            float fraction = stepPosition - step;

            float from = _stepEdgeX(row, step, left: true);
            float to = step + 1 < steps ? _stepEdgeX(row, step + 1, left: true) : _stepEdgeX(row, step, left: false);

            _setWorldX(_playhead, Mathf.Lerp(from, to, fraction));
        }

        private float _stepEdgeX(BeatmakerTrackRow row, int step, bool left)
        {
            var rect = row.GetStepRect(step);
            if (rect == null) return 0f;

            rect.GetWorldCorners(_corners);
            return left ? _corners[0].x : _corners[2].x;
        }

        private static void _setWorldX(Transform target, float x)
        {
            var position = target.position;
            position.x = x;
            target.position = position;
        }

        private static void _setActive(Component target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active)
                target.gameObject.SetActive(active);
        }

        private void _fillPackDropdown(IReadOnlyList<BeatmakerSoundPack> packs, int activePackIndex)
        {
            if (_packDropdown == null) return;

            var options = new List<string>();
            if (packs != null)
            {
                for (int i = 0; i < packs.Count; i++)
                    options.Add(packs[i] != null && !string.IsNullOrEmpty(packs[i].packName)
                        ? packs[i].packName
                        : $"Pack {i + 1}");
            }

            _packDropdown.ClearOptions();
            _packDropdown.AddOptions(options);
            _packDropdown.SetValueWithoutNotify(Mathf.Max(0, activePackIndex));
            _packDropdown.RefreshShownValue();
            _packDropdown.interactable = options.Count > 1;
        }

        private void _wireLoops()
        {
            if (_loopButtons == null) return;

            for (int i = 0; i < _loopButtons.Length; i++)
            {
                if (_loopButtons[i] == null) continue;
                int index = i;
                _loopButtons[i].onClick.AddListener(() => OnLoopToggled?.Invoke(index));
            }
        }

        private void _setLoopLabels(string[] names)
        {
            if (_loopLabels == null) return;

            for (int i = 0; i < _loopLabels.Length; i++)
            {
                if (_loopLabels[i] == null) continue;
                _loopLabels[i].text = (names != null && i < names.Length && !string.IsNullOrEmpty(names[i]))
                    ? names[i]
                    : $"Loop {i + 1}";
            }
        }

        private void _resetVisualState()
        {
            for (int track = 0; track < _rows.Count; track++)
                for (int step = 0; step < BeatmakerPatternState.StepCount; step++)
                    SetStepState(track, step, false);

            if (_loopButtons != null)
                for (int i = 0; i < _loopButtons.Length; i++)
                    SetLoopState(i, false);

            SetMetronomeState(false);
            _lastScore = -1;
            SetScore(0);
            UpdatePlayheads(false, 0f, -1, 0f);
        }

        private static void _setButtonColor(Button button, Color color)
        {
            if (button == null || button.image == null) return;
            button.image.color = color;
        }
    }
}
