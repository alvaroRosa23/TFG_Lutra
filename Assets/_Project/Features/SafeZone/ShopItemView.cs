using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.Features.SafeZone
{
    /// <summary>
    /// Tarjeta de un item en la tienda de la Zona Segura.
    /// </summary>
    public class ShopItemView : MonoBehaviour
    {
        [SerializeField] private Image           _previewImage;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _priceLabel;
        [SerializeField] private Button          _buyButton;

        public void Setup(SafeZoneItem item, Action<SafeZoneItem> onBuy)
        {
            if (_nameLabel != null)
                _nameLabel.text = item.displayName;

            if (_priceLabel != null)
            {
                _priceLabel.text = item.requiredStreakDays > 0
                    ? $"{item.requiredStreakDays} días de racha"
                    : $"{item.coinCost} monedas";
            }

            if (_previewImage != null && item.previewSprite != null)
                _previewImage.sprite = item.previewSprite;

            if (_buyButton != null)
            {
                _buyButton.onClick.RemoveAllListeners();
                SafeZoneItem captured = item;
                _buyButton.onClick.AddListener(() => onBuy?.Invoke(captured));
            }
        }

        private void OnDestroy()
        {
            _buyButton?.onClick.RemoveAllListeners();
        }
    }
}
