using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;

namespace Lutra.Features.EmotionCheck
{
    /// <summary>
    /// Vista del check-in emocional. Gestiona los 5 niveles de ánimo, los tags de emoción
    /// y motivo, la foto opcional y el botón de confirmar.
    /// No contiene lógica de negocio; delega en EmotionCheckController.
    /// </summary>
    public class EmotionCheckView : MonoBehaviour
    {
        // ── Referencias ────────────────────────────────────────────────

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _dateLabel;
        [SerializeField] private Button          _backButton;

        [Header("Mood - 5 caritas")]
        [SerializeField] private Button[] _moodButtons;
        [SerializeField] private Image[]  _moodButtonImages;
        [SerializeField] private Color    _moodSelectedColor;
        [SerializeField] private Color    _moodNormalColor;

        [Header("Emociones")]
        [SerializeField] private Transform   _emotionTagsContainer;
        [SerializeField] private GameObject  _emotionTagPrefab;
        [SerializeField] private Button      _seeMoreEmotionsButton;
        [SerializeField] private GameObject  _extraEmotionsPanel;

        [Header("Motivo")]
        [SerializeField] private Transform      _motiveTagsContainer;
        [SerializeField] private GameObject     _motiveTagPrefab;
        [SerializeField] private Button         _otherMotiveButton;
        [SerializeField] private GameObject     _otherMotiveInputPanel;
        [SerializeField] private TMP_InputField _otherMotiveInput;

        [Header("Foto")]
        [SerializeField] private Button     _addPhotoButton;
        [SerializeField] private Image      _photoPreview;
        [SerializeField] private GameObject _photoPreviewPanel;

        [Header("Confirmar")]
        [SerializeField] private Button          _confirmButton;
        [SerializeField] private TextMeshProUGUI _errorLabel;

        [Header("Tag Colors")]
        [SerializeField] private Color _emotionTagNormalColor   = new Color(0.88f, 0.88f, 0.88f, 1f);
        [SerializeField] private Color _emotionTagSelectedColor  = new Color(0.40f, 0.60f, 1.00f, 1f);
        [SerializeField] private Color _motiveTagNormalColor    = new Color(0.88f, 0.88f, 0.88f, 1f);
        [SerializeField] private Color _motiveTagSelectedColor   = new Color(0.40f, 0.80f, 0.60f, 1f);

        [SerializeField] private EmotionCheckController _controller;

        // ── Estado ─────────────────────────────────────────────────────

        private int          _selectedMoodLevel  = 0;
        private EmotionType? _selectedEmotion = null;
        private bool         _emotionSelected    = false;
        private List<string> _selectedEmotionTags = new();
        private List<string> _selectedMotiveTags  = new();
        private string       _photoPath           = null;

        private List<EmotionTagButton> _emotionTagButtons = new();
        private List<EmotionTagButton> _motiveTagButtons  = new();

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            Debug.Log($"[EmotionCheckView] Awake. " +
                      $"Container: {_emotionTagsContainer != null}, " +
                      $"Prefab: {_emotionTagPrefab != null}");
            _registerMoodButtons();
            _registerActionButtons();
            _registerExtras();
            _updateConfirmButton();
        }

        private void Start()
        {
            LoadEmotionTags();
        }

        private void OnDestroy()
        {
            if (_moodButtons != null)
                foreach (var btn in _moodButtons)
                    btn?.onClick.RemoveAllListeners();

            _backButton?.onClick.RemoveAllListeners();
            _confirmButton?.onClick.RemoveAllListeners();
            _seeMoreEmotionsButton?.onClick.RemoveAllListeners();
            _otherMotiveButton?.onClick.RemoveAllListeners();
            _addPhotoButton?.onClick.RemoveAllListeners();
            _otherMotiveInput?.onEndEdit.RemoveAllListeners();
        }

        // ── API pública ────────────────────────────────────────────────

        public void SetTitle(string title)
        {
            if (_titleLabel != null) _titleLabel.text = title;
        }

        public void SetDate(DateTime date)
        {
            if (_dateLabel != null)
                _dateLabel.text = date.ToString("dd MMMM  HH:mm",
                    new CultureInfo("es-ES"));
        }

        public void ResetAll()
        {
            _selectedMoodLevel = 0;
            _selectedEmotion   = null;
            _emotionSelected   = false;
            _selectedEmotionTags.Clear();
            _selectedMotiveTags.Clear();
            _photoPath = null;
            ClearError();

            if (_extraEmotionsPanel != null)
                _extraEmotionsPanel.SetActive(false);
            if (_otherMotiveInputPanel != null)
            {
                _otherMotiveInputPanel.SetActive(false);
                if (_otherMotiveInput != null)
                    _otherMotiveInput.text = "";
            }
            if (_photoPreviewPanel != null)
                _photoPreviewPanel.SetActive(false);

            if (_moodButtonImages != null)
                foreach (var img in _moodButtonImages)
                    if (img != null) img.color = _moodNormalColor;

            _updateConfirmButton();
            _ = _refreshLayoutNextFrame();
        }

        /// <summary>
        /// Recrea los tags de motivo: primero los hobbies del perfil,
        /// luego los contextos fijos.
        /// </summary>
        public void LoadUserHobbies(List<HobbyType> hobbies)
        {
            if (_motiveTagsContainer == null) return;

            for (int i = _motiveTagsContainer.childCount - 1; i >= 0; i--)
                DestroyImmediate(_motiveTagsContainer.GetChild(i).gameObject);
            Debug.Log($"[LoadUserHobbies] Container limpio. " +
                      $"Hijos restantes: {_motiveTagsContainer.childCount}");
            _motiveTagButtons.Clear();
            _selectedMotiveTags.Clear();

            foreach (var hobby in hobbies)
                _spawnMotiveTag(hobby.ToString(), _getHobbyDisplayName(hobby));

            var fixedTags = new[] { "Trabajo", "Familia", "Salud", "Ocio",
                                    "Relaciones", "Estudio", "Deporte" };
            foreach (var tag in fixedTags)
                _spawnMotiveTag(tag, tag);

            // Forzar recálculo del layout en orden correcto
            Canvas.ForceUpdateCanvases();

            var containers = new Transform[]
            {
                _emotionTagsContainer,
                _motiveTagsContainer,
            };

            foreach (var container in containers)
            {
                if (container == null) continue;
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    container.GetComponent<RectTransform>());

                var parent = container.parent?.GetComponent<RectTransform>();
                if (parent != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            }

            // Recalcular el Content del ScrollView
            var scrollContent = _emotionTagsContainer
                ?.GetComponentInParent<ScrollRect>()
                ?.content;
            if (scrollContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);

            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Instancia los 8 EmotionType como tags: los primeros 6 en el contenedor
        /// principal y los 2 restantes en el panel expandido.
        /// </summary>
        public void LoadEmotionTags()
        {
            Debug.Log($"[EmotionCheckView] LoadEmotionTags called. " +
                      $"Container: {_emotionTagsContainer != null}, " +
                      $"Prefab: {_emotionTagPrefab != null}, " +
                      $"ExtraPanel: {_extraEmotionsPanel != null}");
            if (_emotionTagPrefab == null || _emotionTagsContainer == null) return;

            for (int i = _emotionTagsContainer.childCount - 1; i >= 0; i--)
                DestroyImmediate(_emotionTagsContainer.GetChild(i).gameObject);

            if (_extraEmotionsPanel != null)
                for (int i = _extraEmotionsPanel.transform.childCount - 1; i >= 0; i--)
                    DestroyImmediate(_extraEmotionsPanel.transform.GetChild(i).gameObject);

            Debug.Log($"[LoadEmotionTags] Container limpio. " +
                      $"Hijos restantes: {_emotionTagsContainer.childCount}");

            _emotionTagButtons.Clear();

            var emotions = (EmotionType[])Enum.GetValues(typeof(EmotionType));
            for (int i = 0; i < emotions.Length; i++)
            {
                var emotion  = emotions[i];
                Transform container = (i < 6)
                    ? _emotionTagsContainer
                    : _extraEmotionsPanel?.transform;

                if (container == null) continue;

                var go  = Instantiate(_emotionTagPrefab, container, false);
                var btn = go.GetComponent<EmotionTagButton>();
                if (btn == null) continue;

                string key            = emotion.ToString();
                var    capturedEmotion = emotion;
                btn.Initialize(key, _getEmotionDisplayName(emotion),
                    _emotionTagNormalColor, _emotionTagSelectedColor);

                btn.OnToggled = (value, isSelected) =>
                {
                    if (isSelected)
                    {
                        foreach (var otherBtn in _emotionTagButtons)
                        {
                            if (otherBtn != btn && otherBtn != null)
                                otherBtn.SetSelected(false);
                        }

                        _selectedEmotion = capturedEmotion;
                        _emotionSelected = true;
                        _selectedEmotionTags.Clear();
                        _selectedEmotionTags.Add(value);
                    }
                    else
                    {
                        _selectedEmotionTags.Remove(value);
                        if (_selectedEmotionTags.Count == 0)
                        {
                            _emotionSelected = false;
                            _selectedEmotion = null;
                        }
                    }
                    _updateConfirmButton();
                };

                _emotionTagButtons.Add(btn);
            }

            Debug.Log($"[EmotionCheckView] Tags instanciados: " +
                      $"{_emotionTagsContainer.childCount} hijos en container");

            foreach (Transform child in _emotionTagsContainer)
            {
                Debug.Log($"[EmotionCheckView] Tag: {child.name}, " +
                          $"active: {child.gameObject.activeSelf}, " +
                          $"pos: {child.localPosition}");
            }

            if (_extraEmotionsPanel != null)
                _extraEmotionsPanel.SetActive(false);

            // Forzar recálculo del layout en orden correcto
            Canvas.ForceUpdateCanvases();

            var containers = new Transform[]
            {
                _emotionTagsContainer,
                _motiveTagsContainer,
            };

            foreach (var container in containers)
            {
                if (container == null) continue;
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    container.GetComponent<RectTransform>());

                var parent = container.parent?.GetComponent<RectTransform>();
                if (parent != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            }

            // Recalcular el Content del ScrollView
            var scrollContent = _emotionTagsContainer
                ?.GetComponentInParent<ScrollRect>()
                ?.content;
            if (scrollContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);

            Canvas.ForceUpdateCanvases();
        }

        public void ForceLayoutRefresh()
        {
            Canvas.ForceUpdateCanvases();
            var scrollRect = GetComponentInParent<ScrollRect>();
            if (scrollRect?.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            Canvas.ForceUpdateCanvases();
        }

        public int          GetSelectedMoodLevel()     => _selectedMoodLevel;
        public EmotionType  GetSelectedEmotion()       => _selectedEmotion ?? EmotionType.Calm;
        public bool         IsEmotionSelected()        => _selectedEmotion.HasValue;
        public List<string> GetSelectedEmotionTags()   => _selectedEmotionTags;
        public List<string> GetSelectedMotiveTags()    => _selectedMotiveTags;
        public string       GetPhotoPath()             => _photoPath;

        public void ShowError(string msg)
        {
            if (_errorLabel == null) return;
            _errorLabel.gameObject.SetActive(true);
            _errorLabel.text = msg;
        }

        public void ClearError()
        {
            if (_errorLabel == null) return;
            _errorLabel.text = "";
            _errorLabel.gameObject.SetActive(false);
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _updateConfirmButton()
        {
            if (_confirmButton == null) return;

            bool canConfirm = _selectedMoodLevel > 0 && _emotionSelected;
            _confirmButton.interactable = canConfirm;

            var img = _confirmButton.GetComponent<Image>();
            if (img != null)
                img.color = canConfirm
                    ? Color.white
                    : new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        private void _registerMoodButtons()
        {
            if (_moodButtons == null) return;
            for (int i = 0; i < _moodButtons.Length; i++)
            {
                int capturedLevel = i + 1;
                int capturedIndex = i;
                _moodButtons[i]?.onClick.AddListener(() =>
                    _selectMood(capturedLevel, capturedIndex));
            }
        }

        private void _selectMood(int level, int index)
        {
            Debug.Log($"[EmotionCheckView] Mood seleccionado: level={level}, index={index}");
            _selectedMoodLevel = level;

            if (_moodButtonImages == null) return;
            for (int i = 0; i < _moodButtonImages.Length; i++)
            {
                Debug.Log($"[EmotionCheckView] MoodImage[{i}]: " +
                          $"{(_moodButtonImages[i] != null ? _moodButtonImages[i].name : "NULL")}");

                if (_moodButtonImages[i] != null)
                {
                    Color newColor = (i == index) ? _moodSelectedColor : _moodNormalColor;
                    _moodButtonImages[i].color = newColor;
                    Debug.Log($"[EmotionCheckView] Aplicando color {newColor} a imagen {i}");
                }
            }
            ClearError();
            _updateConfirmButton();
        }

        private void _registerActionButtons()
        {
            _backButton?.onClick.AddListener(() => _controller?.OnBackClicked());

            _confirmButton?.onClick.AddListener(() => _ = _safeConfirm());
        }

        private async Task _safeConfirm()
        {
            try
            {
                if (_controller != null)
                    await _controller.OnConfirmClicked();
            }
            catch (Exception ex)
            { Debug.LogError($"[EmotionCheckView] _confirmButton: {ex.Message}"); }
        }

        private void _registerExtras()
        {
            _seeMoreEmotionsButton?.onClick.AddListener(() =>
            {
                bool newState = !_extraEmotionsPanel.activeSelf;
                _extraEmotionsPanel.SetActive(newState);
                _ = _refreshLayoutNextFrame();
            });

            _otherMotiveButton?.onClick.AddListener(() =>
            {
                bool newState = !_otherMotiveInputPanel.activeSelf;
                _otherMotiveInputPanel.SetActive(newState);
                _ = _refreshLayoutNextFrame();
            });

            _otherMotiveInput?.onEndEdit.AddListener(text =>
            {
                string trimmed = text?.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !_selectedMotiveTags.Contains(trimmed))
                    _selectedMotiveTags.Add(trimmed);
                if (_otherMotiveInput != null)
                    _otherMotiveInput.text = "";
            });

            _addPhotoButton?.onClick.AddListener(() =>
                Debug.Log("[EmotionCheckView] Foto: próximamente"));
        }

        private async Task _refreshLayoutNextFrame()
        {
            await Task.Yield();
            Canvas.ForceUpdateCanvases();
            var scrollRect = GetComponentInParent<ScrollRect>();
            if (scrollRect?.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            Canvas.ForceUpdateCanvases();
        }

        private void _spawnMotiveTag(string value, string displayText)
        {
            if (_motiveTagPrefab == null || _motiveTagsContainer == null) return;

            var go  = Instantiate(_motiveTagPrefab, _motiveTagsContainer, false);
            var btn = go.GetComponent<EmotionTagButton>();
            if (btn == null) return;

            btn.Initialize(value, displayText, _motiveTagNormalColor, _motiveTagSelectedColor);
            btn.OnToggled = (v, isSelected) =>
            {
                if (isSelected)
                {
                    if (!_selectedMotiveTags.Contains(v))
                        _selectedMotiveTags.Add(v);
                }
                else
                {
                    _selectedMotiveTags.Remove(v);
                }
            };
            _motiveTagButtons.Add(btn);
        }

        private static string _getEmotionDisplayName(EmotionType emotion) => emotion switch
        {
            EmotionType.Joy         => "Alegría",
            EmotionType.Calm        => "Calma",
            EmotionType.Sadness     => "Tristeza",
            EmotionType.Anxiety     => "Ansiedad",
            EmotionType.Frustration => "Frustración",
            EmotionType.Overwhelm   => "Agobio",
            EmotionType.Nostalgia   => "Nostalgia",
            EmotionType.Energy      => "Energía",
            _                       => emotion.ToString()
        };

        private static string _getHobbyDisplayName(HobbyType hobby) => hobby switch
        {
            HobbyType.Football     => "Fútbol",
            HobbyType.Basketball   => "Baloncesto",
            HobbyType.Tennis       => "Tenis",
            HobbyType.Swimming     => "Natación",
            HobbyType.Cycling      => "Ciclismo",
            HobbyType.Running      => "Running",
            HobbyType.Yoga         => "Yoga",
            HobbyType.Dancing      => "Baile",
            HobbyType.Cooking      => "Cocina",
            HobbyType.Reading      => "Lectura",
            HobbyType.Gaming       => "Videojuegos",
            HobbyType.Music        => "Música",
            HobbyType.Drawing      => "Dibujo",
            HobbyType.Photography  => "Fotografía",
            HobbyType.Traveling    => "Viajes",
            HobbyType.Hiking       => "Senderismo",
            HobbyType.Meditation   => "Meditación",
            HobbyType.Writing      => "Escritura",
            HobbyType.Cinema       => "Cine",
            HobbyType.Theater      => "Teatro",
            HobbyType.Crafts       => "Manualidades",
            HobbyType.Gardening    => "Jardinería",
            HobbyType.Volunteering => "Voluntariado",
            HobbyType.Fitness      => "Fitness",
            HobbyType.Surfing      => "Surf",
            _                      => hobby.ToString()
        };
    }
}
