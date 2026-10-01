using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;

namespace Lutra.Features.StarCollection
{
    /// <summary>Datos de una fila del libro de colección.</summary>
    public struct StarBookEntryData
    {
        public StarDefinition Star;
        public bool           Discovered;
        public int            TimesCaught;
        public Color          RarityColor;
    }

    /// <summary>
    /// Fila del libro de colección: icono, nombre (??? si no se ha descubierto), rareza y veces
    /// pescada. La estrella de racha sin descubrir muestra cómo se consigue.
    /// </summary>
    public class StarBookEntryView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image           _icon;
        [SerializeField] private Image           _rarityStripe;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _subtitleLabel;
        [SerializeField] private TextMeshProUGUI _countLabel;

        [SerializeField] private Color _undiscoveredIconColor = new Color(0.08f, 0.08f, 0.15f, 0.9f);

        private StarBookEntryData _data;
        private Action<StarBookEntryData> _onClicked;

        public void Setup(StarBookEntryData data, int streakDays, Action<StarBookEntryData> onClicked)
        {
            _data      = data;
            _onClicked = onClicked;

            var star = data.Star;
            if (_icon != null)
            {
                _icon.sprite = StarSpriteFactory.Or(star.sprite);
                _icon.color  = data.Discovered ? (star.sprite != null ? Color.white : star.tint) : _undiscoveredIconColor;
                _icon.preserveAspect = true;
            }

            if (_rarityStripe != null) _rarityStripe.color = data.Discovered ? data.RarityColor : _undiscoveredIconColor;
            if (_nameLabel != null)    _nameLabel.text = data.Discovered ? star.displayName : "???";

            if (_subtitleLabel != null)
            {
                _subtitleLabel.text = data.Discovered
                    ? star.rarity.ToDisplayName()
                    : star.isStreakSpecial ? $"Se desbloquea en el {streakDays}º día de racha" : "Sin descubrir";
            }

            if (_countLabel != null) _countLabel.text = data.Discovered ? $"x{data.TimesCaught}" : string.Empty;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_data.Discovered) _onClicked?.Invoke(_data);
        }
    }
}
