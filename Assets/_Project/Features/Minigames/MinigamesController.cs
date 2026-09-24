using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Data.ScriptableObjects;
using Lutra.Core.Events;
using Lutra.Minigames;
using Lutra.UI.Components;

namespace Lutra.Features.Minigames
{
    /// <summary>
    /// Controlador de la sección de minijuegos.
    /// Añadir un nuevo minijuego solo requiere crear un MinigameDefinition SO
    /// y añadirlo al array _allMinigames en el Inspector.
    /// </summary>
    public class MinigamesController : MonoBehaviour
    {
        [SerializeField] private MinigamesView        _view;
        [SerializeField] private MinigameDefinition[] _allMinigames;
        [SerializeField] private EmotionMinigameMap   _emotionMap;

        // ── Servicios (lazy) ───────────────────────────────────────────

        private DataRepository _dataRepo;
        private DataRepository DataRepo => _dataRepo ??= ServiceLocator.Get<DataRepository>();

        private MinigameLoader _minigameLoader;
        private MinigameLoader MinigameLoader => _minigameLoader ??= ServiceLocator.Get<MinigameLoader>();

        // ── Estado ─────────────────────────────────────────────────────

        private EmotionType  _currentEmotion;
        private string       _currentSearch = string.Empty;
        private MinigameTag? _currentFilter;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null)
                _view = GetComponentInChildren<MinigamesView>();
        }

        private void OnEnable()
        {
            EventBus.OnCurrentEmotionChanged += _onCurrentEmotionChanged;
        }

        private void OnDisable()
        {
            EventBus.OnCurrentEmotionChanged -= _onCurrentEmotionChanged;
            _unsubscribeFromView();
        }

        // ── API pública ────────────────────────────────────────────────

        public async Task OpenMinigames()
        {
            try
            {
                _view?.HideDetailPanel();
                _view?.HideFilterDropdown();
                _currentSearch = string.Empty;
                _currentFilter = null;

                var lastRecord  = await DataRepo.GetLastEmotion();
                _currentEmotion = lastRecord != null ? lastRecord.EmotionType : EmotionType.Calm;

                var all         = new List<MinigameDefinition>(_allMinigames ?? Array.Empty<MinigameDefinition>());
                var recommended = _getRecommended(_currentEmotion);

                _unsubscribeFromView();
                _subscribeToView();

                _view?.ShowMinigames(all, recommended);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MinigamesController] OpenMinigames: {ex.Message}\n{ex.StackTrace}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        /// <summary>Inicia la escena del minijuego. Preparado para cuando esté implementado.</summary>
        public async Task StartMinigame(MinigameType type)
        {
            try
            {
                await MinigameLoader.LoadMinigame(type, _currentEmotion);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MinigamesController] StartMinigame: {ex.Message}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        // ── Handlers de vista ──────────────────────────────────────────

        private void _onSearchChanged(string query)
        {
            _currentSearch = query ?? string.Empty;
            _view?.UpdateAllSection(_getFilteredAll(_currentSearch, _currentFilter));
            _view?.UpdateRecommendedSection(_applyFilters(_getRecommended(_currentEmotion), _currentSearch, _currentFilter));
        }

        private void _onFilterChanged(MinigameTag? tag)
        {
            _currentFilter = tag;
            _view?.UpdateAllSection(_getFilteredAll(_currentSearch, _currentFilter));
            _view?.UpdateRecommendedSection(_applyFilters(_getRecommended(_currentEmotion), _currentSearch, _currentFilter));
        }

        private void _onCardSelected(MinigameDefinition def)
            => _ = _safeShowDetail(def);

        private async Task _safeShowDetail(MinigameDefinition def)
        {
            try
            {
                string record = await _getRecordText(def.minigameType);
                _view?.ShowDetailPanel(def, record);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MinigamesController] _safeShowDetail: {ex.Message}");
            }
        }

        private void _onPlayRequested(MinigameType type) => _ = StartMinigame(type);

        private void _onFilterDropdownRequested()
        {
            _view?.ShowFilterDropdown(_getDistinctTags());
        }

        private void _onCurrentEmotionChanged(EmotionType emotion)
        {
            _currentEmotion = emotion;
            _view?.UpdateRecommendedSection(_applyFilters(_getRecommended(emotion), _currentSearch, _currentFilter));
        }

        // ── Suscripciones ──────────────────────────────────────────────

        private void _subscribeToView()
        {
            if (_view == null) return;
            _view.OnSearchChanged           += _onSearchChanged;
            _view.OnFilterChanged           += _onFilterChanged;
            _view.OnCardSelected            += _onCardSelected;
            _view.OnPlayRequested           += _onPlayRequested;
            _view.OnFilterDropdownRequested += _onFilterDropdownRequested;
        }

        private void _unsubscribeFromView()
        {
            if (_view == null) return;
            _view.OnSearchChanged           -= _onSearchChanged;
            _view.OnFilterChanged           -= _onFilterChanged;
            _view.OnCardSelected            -= _onCardSelected;
            _view.OnPlayRequested           -= _onPlayRequested;
            _view.OnFilterDropdownRequested -= _onFilterDropdownRequested;
        }

        // ── Helpers ────────────────────────────────────────────────────

        private List<MinigameDefinition> _getRecommended(EmotionType emotion)
        {
            if (_allMinigames == null || _emotionMap == null)
                return new List<MinigameDefinition>();

            var tags   = _emotionMap.GetRecommendedTags(emotion);
            var result = new List<MinigameDefinition>();

            foreach (var def in _allMinigames)
            {
                if (def == null || def.tags == null) continue;
                foreach (var tag in def.tags)
                {
                    if (Array.IndexOf(tags, tag) >= 0)
                    {
                        result.Add(def);
                        break;
                    }
                }
            }

            return result;
        }

        private List<MinigameDefinition> _getFilteredAll(string query, MinigameTag? filterTag)
            => _applyFilters(new List<MinigameDefinition>(_allMinigames ?? Array.Empty<MinigameDefinition>()), query, filterTag);

        private static List<MinigameDefinition> _applyFilters(List<MinigameDefinition> source, string query, MinigameTag? filterTag)
        {
            var result = new List<MinigameDefinition>();

            foreach (var def in source)
            {
                if (def == null) continue;

                if (!string.IsNullOrEmpty(query) &&
                    !def.displayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (filterTag.HasValue)
                {
                    if (def.tags == null) continue;
                    bool hasTag = false;
                    foreach (var tag in def.tags)
                        if (tag == filterTag.Value) { hasTag = true; break; }
                    if (!hasTag) continue;
                }

                result.Add(def);
            }

            return result;
        }

        private List<MinigameTag> _getDistinctTags()
        {
            var result = new List<MinigameTag>();
            var seen   = new HashSet<MinigameTag>();

            if (_allMinigames == null) return result;

            foreach (var def in _allMinigames)
            {
                if (def?.tags == null) continue;
                foreach (var tag in def.tags)
                    if (seen.Add(tag))
                        result.Add(tag);
            }

            return result;
        }

        private async Task<string> _getRecordText(MinigameType type)
        {
            float? best = await DataRepo.GetBestRelaxationScore(type);
            return best.HasValue
                ? $"Mejor puntuación: {MinigameOutcome.ToDisplayScore(best.Value)}"
                : "Sin récord";
        }
    }
}
