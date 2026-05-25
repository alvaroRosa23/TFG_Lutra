using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Lutra.Features.SafeZone
{
    /// <summary>
    /// Icono de un item del inventario en la barra inferior.
    /// Solo muestra imagen y nombre; al pulsarse notifica para abrir el popup de opciones.
    /// El click se detecta vía IPointerClickHandler en el GameObject raíz del prefab.
    /// </summary>
    public class InventoryItemView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image           _previewImage;
        [SerializeField] private TextMeshProUGUI _nameLabel;

        private SafeZoneItem                               _item;
        private Action<SafeZoneItem, RectTransform>        _onSelected;
        private RectTransform                              _rectTransform;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        public void Setup(SafeZoneItem item, Action<SafeZoneItem, RectTransform> onSelected)
        {
            _item       = item;
            _onSelected = onSelected;

            if (_nameLabel != null)
                _nameLabel.text = item.displayName;

            if (_previewImage != null && item.previewSprite != null)
                _previewImage.sprite = item.previewSprite;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onSelected?.Invoke(_item, _rectTransform);
        }
    }
}
