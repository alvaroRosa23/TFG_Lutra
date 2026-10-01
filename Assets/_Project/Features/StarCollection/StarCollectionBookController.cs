using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;

namespace Lutra.Features.StarCollection
{
    /// <summary>
    /// Controlador del libro de colección. Abre el libro con la colección actual (la que pasa el
    /// minijuego, ya al día en memoria, o una cargada de SQLite) y gestiona el filtro por rareza.
    /// Orden: por rareza (de Común a Legendaria) y, dentro de cada una, el orden del catálogo;
    /// la estrella de racha va al final.
    /// </summary>
    public class StarCollectionBookController : MonoBehaviour
    {
        [SerializeField] private StarCollectionBookView _view;
        [SerializeField] private StarCatalog            _catalog;
        [Tooltip("Días de racha de la estrella especial (solo para el texto de pista)")]
        [SerializeField] private int                    _streakStarDays = 5;

        private StarCollectionStore _collection;
        private StarRarity?    _filter;
        private Action         _onClosed;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (_view == null) _view = GetComponentInChildren<StarCollectionBookView>(true);
        }

        private void OnEnable()
        {
            if (_view == null) return;
            _view.OnCloseRequested += Close;
            _view.OnFilterSelected += _onFilterSelected;
            _view.OnEntrySelected  += _onEntrySelected;
        }

        private void OnDisable()
        {
            if (_view == null) return;
            _view.OnCloseRequested -= Close;
            _view.OnFilterSelected -= _onFilterSelected;
            _view.OnEntrySelected  -= _onEntrySelected;
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Abre el libro. Si no se pasa colección se carga de la BD. onClosed se llama al cerrar.
        /// </summary>
        public void Open(StarCollectionStore collection = null, Action onClosed = null)
            => _ = _safeOpen(collection, onClosed);

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _view?.Show(false);

            var callback = _onClosed;
            _onClosed = null;
            callback?.Invoke();
        }

        // ── Privado ────────────────────────────────────────────────────

        private async Task _safeOpen(StarCollectionStore collection, Action onClosed)
        {
            try
            {
                if (_view == null || _catalog == null)
                {
                    Debug.LogError("[StarCollectionBookController] Falta asignar la vista o el catálogo");
                    onClosed?.Invoke();
                    return;
                }

                _onClosed = onClosed;
                IsOpen    = true;
                _filter   = null;

                _collection = collection;
                if (_collection == null || !_collection.IsLoaded)
                {
                    _collection = new StarCollectionStore(_catalog);
                    await _collection.LoadAsync();
                }

                if (!IsOpen) return; // se cerró mientras cargaba

                _view.Show(true);
                _refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StarCollectionBookController] {ex.Message}");
            }
        }

        private void _onFilterSelected(StarRarity? filter)
        {
            _filter = filter;
            _refresh();
        }

        private void _onEntrySelected(StarBookEntryData data) => _view.ShowDetail(data);

        private void _refresh()
        {
            if (_collection == null) return;

            var entries = new List<StarBookEntryData>();
            for (int r = 0; r < StarRarityExtensions.Count; r++)
            {
                var rarity = (StarRarity)r;
                if (_filter.HasValue && _filter.Value != rarity) continue;

                foreach (var star in _catalog.GetRegularStars())
                    if (star.rarity == rarity) entries.Add(_buildEntry(star));
            }

            var streakStar = _catalog.GetStreakStar();
            if (streakStar != null && (!_filter.HasValue || _filter.Value == streakStar.rarity))
                entries.Add(_buildEntry(streakStar));

            _view.SetFilter(_filter);
            _view.SetCounter(_collection.DiscoveredRegularCount, _collection.RegularCount);
            _view.ShowEntries(entries, _streakStarDays);
        }

        private StarBookEntryData _buildEntry(StarDefinition star) => new StarBookEntryData
        {
            Star        = star,
            Discovered  = _collection.IsDiscovered(star.starId),
            TimesCaught = _collection.GetTimesCaught(star.starId),
            RarityColor = _catalog.GetRarityColor(star.rarity)
        };
    }
}
