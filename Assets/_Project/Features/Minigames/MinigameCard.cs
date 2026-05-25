using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.ScriptableObjects;

namespace Lutra.Features.Minigames
{
    /// <summary>
    /// Componente de la card de selección de minijuego.
    /// El prefab debe tener este componente y los campos serializados asignados.
    /// </summary>
    public class MinigameCard : MonoBehaviour
    {
        [SerializeField] private Image           _logoImage;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _timeNumberLabel;
        [SerializeField] private TextMeshProUGUI _timeUnitLabel;
        [SerializeField] private Transform       _tagsContainer;
        [SerializeField] private GameObject      _tagPrefab;
        [SerializeField] private Button          _cardButton;

        public void Setup(MinigameDefinition definition, Action onClicked)
        {
            if (_logoImage != null && definition.logo != null)
                _logoImage.sprite = definition.logo;

            if (_nameLabel != null)
                _nameLabel.text = definition.displayName;

            _applyTime(definition.estimatedTimeSeconds);

            _populateTags(definition);

            if (_cardButton != null)
            {
                _cardButton.onClick.RemoveAllListeners();
                _cardButton.onClick.AddListener(() => onClicked?.Invoke());
                _cardButton.interactable = definition.isAvailable;
            }
        }

        private void OnDestroy()
        {
            _cardButton?.onClick.RemoveAllListeners();
        }

        private void _populateTags(MinigameDefinition definition)
        {
            if (_tagsContainer == null || _tagPrefab == null) return;

            for (int i = _tagsContainer.childCount - 1; i >= 0; i--)
            {
                var child = _tagsContainer.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            if (definition.tags == null) return;

            foreach (var tag in definition.tags)
            {
                var tagObj = Instantiate(_tagPrefab, _tagsContainer, false);
                var label  = tagObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = tag.ToString();
            }
        }

        private void _applyTime(int seconds)
        {
            bool inMinutes = seconds >= 60;
            if (_timeNumberLabel != null)
                _timeNumberLabel.text = inMinutes ? (seconds / 60).ToString() : seconds.ToString();
            if (_timeUnitLabel != null)
                _timeUnitLabel.text = inMinutes ? "min" : "seg";
        }
    }
}
