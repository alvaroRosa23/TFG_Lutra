using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

namespace Lutra.Minigames
{
    /// <summary>
    /// Fila del secuenciador (prefab): etiqueta del sonido + 8 botones de step.
    /// BeatmakerView instancia una fila por pista y escucha OnStepClicked.
    /// </summary>
    public class BeatmakerTrackRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [FormerlySerializedAs("_beatButtons")] [SerializeField] private Button[] _stepButtons = new Button[BeatmakerPatternState.StepCount];

        /// <summary>Índice del step pulsado (0-7).</summary>
        public event Action<int> OnStepClicked;

        public int StepCount => _stepButtons != null ? _stepButtons.Length : 0;

        private void Awake()
        {
            if (_stepButtons == null) return;

            for (int i = 0; i < _stepButtons.Length; i++)
            {
                if (_stepButtons[i] == null) continue;
                int step = i; // captura local: evita el bug de cierre sobre variable de bucle
                _stepButtons[i].onClick.AddListener(() => OnStepClicked?.Invoke(step));
            }
        }

        private void OnDestroy() => OnStepClicked = null;

        public void SetLabel(string text)
        {
            if (_label != null) _label.text = text;
        }

        public void SetStepColor(int step, Color color)
        {
            if (_stepButtons == null || step < 0 || step >= _stepButtons.Length) return;

            var button = _stepButtons[step];
            if (button == null || button.image == null) return;
            button.image.color = color;
        }

        public RectTransform GetStepRect(int step)
        {
            if (_stepButtons == null || step < 0 || step >= _stepButtons.Length) return null;
            return _stepButtons[step] != null ? _stepButtons[step].transform as RectTransform : null;
        }
    }
}
