using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lutra.Features.SafeZone
{
    /// <summary>
    /// Punto de la habitación donde se puede colocar un item decorativo.
    /// Acepta drops de DraggableItem y clics para gestionar items ya colocados.
    /// Asignar en el Inspector de SafeZoneView.
    /// </summary>
    public class PlacementPoint : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        [Header("Configuración")]
        public int             placementIndex;
        public PlacementType[] acceptedTypes;

        [Header("Referencias")]
        [SerializeField] private Transform _itemAnchor;
        [SerializeField] private Image     _highlightImage;

        public bool   IsOccupied     { get; private set; }
        public string OccupiedItemId { get; private set; }

        /// <summary>Se dispara cuando se recibe un drop de DraggableItem.</summary>
        public event Action<int, string> OnDropReceived;   // (placementIndex, itemId)

        /// <summary>Se dispara cuando el usuario toca este punto.</summary>
        public event Action<int> OnTapped;                 // (placementIndex)

        private bool       _placementModeActive;
        private GameObject _placedItemGo;

        public Transform ItemAnchor => _itemAnchor != null ? _itemAnchor : transform;

        private static readonly Color ColorEmpty    = Color.white;
        private static readonly Color ColorOccupied = new Color(0.3f, 0.6f, 1f, 0.55f);
        private static readonly Color ColorValid    = new Color(0f,   0.8f, 0f, 0.55f);
        private static readonly Color ColorInvalid  = new Color(1f,   0f,   0f, 0.45f);

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Start() => _refreshHighlight();

        // ── API pública ────────────────────────────────────────────────

        public void SetOccupied(bool occupied, string itemId = null, GameObject prefab = null)
        {
            IsOccupied     = occupied;
            OccupiedItemId = occupied ? itemId : null;

            if (_placedItemGo != null)
            {
                _placedItemGo.SetActive(false);
                Destroy(_placedItemGo);
                _placedItemGo = null;
            }

            if (occupied && prefab != null)
                _placedItemGo = Instantiate(prefab, ItemAnchor, false);

            if (!_placementModeActive)
                _refreshHighlight();
        }

        public void SetPlacementMode(bool active, PlacementType pendingType)
        {
            _placementModeActive = active;

            if (!active) { _refreshHighlight(); return; }

            if (_highlightImage == null) return;
            bool isValid = !IsOccupied && AcceptsType(pendingType);
            _highlightImage.enabled = true;
            _highlightImage.color   = isValid ? ColorValid : ColorInvalid;
        }

        private void _refreshHighlight()
        {
            if (_highlightImage == null) return;
            _highlightImage.enabled = true;
            _highlightImage.color   = IsOccupied ? ColorOccupied : ColorEmpty;
        }

        public bool AcceptsType(PlacementType type)
        {
            if (acceptedTypes == null || acceptedTypes.Length == 0) return true;
            foreach (var t in acceptedTypes)
                if (t == type) return true;
            return false;
        }

        // ── IDropHandler ───────────────────────────────────────────────

        public void OnDrop(PointerEventData eventData)
        {
            var draggable = eventData.pointerDrag?.GetComponent<DraggableItem>();
            if (draggable == null || IsOccupied) return;
            if (!AcceptsType(draggable.PlacementType)) return;

            OnDropReceived?.Invoke(placementIndex, draggable.ItemId);
        }

        // ── IPointerClickHandler ───────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            OnTapped?.Invoke(placementIndex);
        }
    }
}
