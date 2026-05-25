using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lutra.Features.SafeZone
{
    /// <summary>
    /// Permite arrastrar un item del inventario hasta un PlacementPoint de la habitación.
    /// Añadir dinámicamente a los InventoryItemView que se instancian en el panel de inventario.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public string        ItemId        { get; private set; }
        public PlacementType PlacementType { get; private set; }

        /// <summary>Se dispara al iniciar el drag (para que la vista entre en modo colocación).</summary>
        public event Action<DraggableItem> OnDragStarted;
        /// <summary>Se dispara al soltar sin drop válido.</summary>
        public event Action<DraggableItem> OnDragCancelled;

        private RectTransform _rectTransform;
        private CanvasGroup   _canvasGroup;
        private Canvas        _rootCanvas;
        private Transform     _originalParent;
        private Vector2       _originalAnchoredPos;

        // ── Setup ──────────────────────────────────────────────────────

        public void Setup(string itemId, PlacementType type, Canvas rootCanvas)
        {
            ItemId        = itemId;
            PlacementType = type;

            _rootCanvas = rootCanvas;
            if (_rootCanvas == null)
                _rootCanvas = GetComponentInParent<Canvas>();

            _rectTransform = GetComponent<RectTransform>();

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // ── Drag handlers ──────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_rectTransform == null || _canvasGroup == null || _rootCanvas == null)
            {
                Debug.LogError("[DraggableItem] Setup() no fue llamado. Asegúrate de llamar Setup() tras AddComponent.");
                return;
            }

            _originalParent      = transform.parent;
            _originalAnchoredPos = _rectTransform.anchoredPosition;

            transform.SetParent(_rootCanvas.transform, true);
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha          = 0.75f;

            OnDragStarted?.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_rectTransform == null || _rootCanvas == null) return;
            _rectTransform.anchoredPosition += eventData.delta / _rootCanvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_canvasGroup == null || _originalParent == null || _rectTransform == null) return;

            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha          = 1f;

            transform.SetParent(_originalParent, true);
            _rectTransform.anchoredPosition = _originalAnchoredPos;

            OnDragCancelled?.Invoke(this);
        }
    }
}
