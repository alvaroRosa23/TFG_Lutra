using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Events;
using Lutra.Core.Systems;
using Lutra.UI.Components;

namespace Lutra.Features.Diary
{
    /// <summary>
    /// Controlador del Diario Personal.
    /// Muestra las entradas mes a mes (desde el mes de creación de la cuenta hasta el actual),
    /// gestiona el editor, el borrado con confirmación y la búsqueda.
    /// </summary>
    public class DiaryController : MonoBehaviour
    {
        [SerializeField] private DiaryView _view;

        private static readonly CultureInfo Spanish = new CultureInfo("es-ES");

        // ── Servicios ──────────────────────────────────────────────────

        private DataRepository _dataRepository;
        private DataRepository Repo => _dataRepository ??= ServiceLocator.Get<DataRepository>();

        // ── Estado ─────────────────────────────────────────────────────

        // Todas las entradas, de la más reciente a la más antigua
        private List<DiaryEntry> _entries = new();
        private DiaryEntry       _editingEntry;
        private DiaryEntry       _pendingDelete;

        // Día 1 del primer mes navegable, del mes mostrado y del mes actual
        private DateTime _firstMonth   = _monthOf(DateTime.Today);
        private DateTime _shownMonth   = _monthOf(DateTime.Today);
        private DateTime _currentMonth = _monthOf(DateTime.Today);

        private string _searchQuery = string.Empty;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null)
                _view = GetComponentInChildren<DiaryView>();
        }

        private void OnEnable()  => _subscribeToView();
        private void OnDisable() => _unsubscribeFromView();

        // ── API pública ────────────────────────────────────────────────

        public async Task OpenDiary()
        {
            try
            {
                _editingEntry  = null;
                _pendingDelete = null;
                _searchQuery   = string.Empty;
                _view?.ClearSearch();
                _view?.HideDeleteConfirm();
                _showMainView();

                await _reloadEntries();
                await _loadMonthRange();
                _shownMonth = _currentMonth;
                _refreshList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DiaryController] OpenDiary: {ex.Message}\n{ex.StackTrace}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        public async Task OpenNewEntry()
        {
            try
            {
                var entry = new DiaryEntry
                {
                    Date  = DateTime.Now,
                    Title = string.Empty
                };

                var todayEmotion = await Repo.GetTodayEmotion();
                if (todayEmotion != null)
                    entry.Mood = todayEmotion.EmotionType.ToString();

                _showEditorView(entry);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DiaryController] OpenNewEntry: {ex.Message}\n{ex.StackTrace}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        public Task OpenEntry(DiaryEntry entry)
        {
            _showEditorView(entry);
            return Task.CompletedTask;
        }

        public async Task SaveEntry(DiaryEntry entry)
        {
            try
            {
                string title   = _view?.GetTitle()   ?? string.Empty;
                string content = _view?.GetContent() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(content))
                {
                    _view?.ShowError("Escribe algo en tu entrada");
                    return;
                }

                // Se copia el texto solo al guardar: si se vuelve atrás la entrada queda intacta
                bool isNew    = entry.Id == 0;
                entry.Title   = title;
                entry.Content = content;

                await Repo.SaveDiaryEntry(entry);
                if (isNew) _ = _grantDiaryReward();

                await _reloadEntries();

                _editingEntry = null;
                _shownMonth   = _monthOf(entry.Date);
                _showMainView();
                _refreshList();

                EventBus.EmitDiaryEntrySaved(entry);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DiaryController] SaveEntry: {ex.Message}\n{ex.StackTrace}");
                _view?.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        /// <summary>Borra la entrada en SQLite y en Firestore (DataRepository sube el borrado).</summary>
        public async Task DeleteEntry(DiaryEntry entry)
        {
            try
            {
                await Repo.DeleteDiaryEntry(entry);
                await _reloadEntries();
                _refreshList();
                ToastNotification.ShowSuccess("Entrada borrada");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DiaryController] DeleteEntry: {ex.Message}\n{ex.StackTrace}");
                ToastNotification.ShowError("No se pudo borrar la entrada");
            }
        }

        public async Task<DiaryEntry> GetEntryForDate(DateTime date)
        {
            try
            {
                var cached = _entries.FirstOrDefault(e => e.Date.Date == date.Date);
                if (cached != null) return cached;

                return await Repo.GetDiaryEntryByDate(date);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DiaryController] GetEntryForDate: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        // ── Suscripciones ──────────────────────────────────────────────

        private void _subscribeToView()
        {
            if (_view == null) return;

            _view.OnNewEntryClicked      += _onNewEntryClicked;
            _view.OnSaveClicked          += _onSaveClicked;
            _view.OnBackClicked          += _onBackClicked;
            _view.OnSearchChanged        += _onSearchChanged;
            _view.OnPreviousMonthClicked += _onPreviousMonthClicked;
            _view.OnNextMonthClicked     += _onNextMonthClicked;
            _view.OnEditEntryClicked     += _onEditEntryClicked;
            _view.OnDeleteEntryClicked   += _onDeleteEntryClicked;
            _view.OnDeleteConfirmed      += _onDeleteConfirmed;
            _view.OnDeleteCancelled      += _onDeleteCancelled;
        }

        private void _unsubscribeFromView()
        {
            if (_view == null) return;

            _view.OnNewEntryClicked      -= _onNewEntryClicked;
            _view.OnSaveClicked          -= _onSaveClicked;
            _view.OnBackClicked          -= _onBackClicked;
            _view.OnSearchChanged        -= _onSearchChanged;
            _view.OnPreviousMonthClicked -= _onPreviousMonthClicked;
            _view.OnNextMonthClicked     -= _onNextMonthClicked;
            _view.OnEditEntryClicked     -= _onEditEntryClicked;
            _view.OnDeleteEntryClicked   -= _onDeleteEntryClicked;
            _view.OnDeleteConfirmed      -= _onDeleteConfirmed;
            _view.OnDeleteCancelled      -= _onDeleteCancelled;
        }

        // ── Handlers de vista ──────────────────────────────────────────

        private void _onNewEntryClicked()                  => _ = _safeOpenNewEntry();
        private void _onSaveClicked()                      => _ = _safeSaveEntry();
        private void _onEditEntryClicked(DiaryEntry entry) => _ = _safeOpenEntry(entry);

        private void _onBackClicked()
        {
            _editingEntry = null;
            _showMainView();
        }

        private void _onPreviousMonthClicked()
        {
            if (_shownMonth <= _firstMonth) return;
            _shownMonth = _shownMonth.AddMonths(-1);
            _refreshList();
        }

        private void _onNextMonthClicked()
        {
            if (_shownMonth >= _currentMonth) return;
            _shownMonth = _shownMonth.AddMonths(1);
            _refreshList();
        }

        private void _onDeleteEntryClicked(DiaryEntry entry)
        {
            _pendingDelete = entry;
            _view?.ShowDeleteConfirm(entry);
        }

        private void _onDeleteConfirmed()
        {
            _view?.HideDeleteConfirm();
            if (_pendingDelete == null) return;

            var entry = _pendingDelete;
            _pendingDelete = null;
            _ = _safeDeleteEntry(entry);
        }

        private void _onDeleteCancelled()
        {
            _pendingDelete = null;
            _view?.HideDeleteConfirm();
        }

        private void _onSearchChanged(string query)
        {
            _searchQuery = query ?? string.Empty;
            _refreshList();
        }

        // ── Lista y navegación ─────────────────────────────────────────

        /// <summary>
        /// Con búsqueda muestra las coincidencias de todos los meses; sin ella, las entradas del
        /// mes seleccionado.
        /// </summary>
        private void _refreshList()
        {
            if (_view == null) return;

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                string lower    = _searchQuery.Trim().ToLowerInvariant();
                var    filtered = _entries
                    .Where(e =>
                        (e.Title   != null && e.Title.ToLowerInvariant().Contains(lower)) ||
                        (e.Content != null && e.Content.ToLowerInvariant().Contains(lower)))
                    .ToList();

                _view.SetMonthNavigation("Resultados", false, false);
                _view.RefreshEntries(filtered, "No se encontraron entradas");
                return;
            }

            if (_shownMonth < _firstMonth)   _shownMonth = _firstMonth;
            if (_shownMonth > _currentMonth) _shownMonth = _currentMonth;

            var monthEntries = _entries
                .Where(e => e.Date.Year == _shownMonth.Year && e.Date.Month == _shownMonth.Month)
                .ToList();

            _view.SetMonthNavigation(_formatMonth(_shownMonth), _shownMonth > _firstMonth, _shownMonth < _currentMonth);
            _view.RefreshEntries(monthEntries, "No hubo entradas este mes");
        }

        /// <summary>
        /// El historial empieza en el mes de creación de la cuenta (o en el de la entrada más
        /// antigua si es anterior, p. ej. restaurada) y termina en el mes actual.
        /// </summary>
        private async Task _loadMonthRange()
        {
            _currentMonth = _monthOf(DateTime.Today);
            var first = _currentMonth;

            var profile = await Repo.GetUserProfile();
            if (profile != null && profile.CreationDate != default && profile.CreationDate < first)
                first = _monthOf(profile.CreationDate);

            if (_entries.Count > 0)
            {
                var oldest = _monthOf(_entries.Min(e => e.Date));
                if (oldest < first) first = oldest;
            }

            _firstMonth = first;
        }

        private async Task _reloadEntries()
        {
            var entries = await Repo.GetAllDiaryEntries();
            entries.Reverse(); // GetAllDiaryEntries viene de la más antigua a la más reciente
            _entries = entries;
        }

        private void _showMainView()
        {
            _view?.ShowMainView();
            EventBus.EmitNavBarVisibilityRequested(true);
        }

        private void _showEditorView(DiaryEntry entry)
        {
            _editingEntry = entry;
            _view?.ShowEditorView(entry);
            EventBus.EmitNavBarVisibilityRequested(false);
        }

        private static DateTime _monthOf(DateTime date) => new DateTime(date.Year, date.Month, 1);

        /// <summary>"Abril 2026"</summary>
        private static string _formatMonth(DateTime month)
        {
            string text = month.ToString("MMMM yyyy", Spanish);
            return string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);
        }

        // ── Fire-and-forget wrappers ───────────────────────────────────

        private async Task _safeOpenNewEntry()
        {
            try   { await OpenNewEntry(); }
            catch (Exception ex) { Debug.LogError($"[DiaryController] OpenNewEntry: {ex.Message}"); }
        }

        private async Task _safeSaveEntry()
        {
            try
            {
                if (_editingEntry == null) return;
                await SaveEntry(_editingEntry);
            }
            catch (Exception ex) { Debug.LogError($"[DiaryController] SaveEntry: {ex.Message}"); }
        }

        private async Task _safeOpenEntry(DiaryEntry entry)
        {
            try   { await OpenEntry(entry); }
            catch (Exception ex) { Debug.LogError($"[DiaryController] OpenEntry: {ex.Message}"); }
        }

        private async Task _safeDeleteEntry(DiaryEntry entry)
        {
            try   { await DeleteEntry(entry); }
            catch (Exception ex) { Debug.LogError($"[DiaryController] DeleteEntry: {ex.Message}"); }
        }

        /// <summary>
        /// Otorga 5 monedas la primera vez que el usuario escribe en el diario cada día.
        /// Usa Preferences["lastDiaryRewardDate"] para evitar granjas.
        /// </summary>
        private async Task _grantDiaryReward()
        {
            try
            {
                var profile = await Repo.GetUserProfile();
                if (profile == null) return;

                var prefs    = profile.Preferences ?? new Dictionary<string, string>();
                string today = DateTime.Today.ToString("yyyy-MM-dd");

                if (prefs.TryGetValue("lastDiaryRewardDate", out var last) && last == today)
                    return;

                await Repo.AddCoins(5);

                prefs["lastDiaryRewardDate"] = today;
                profile.Preferences          = prefs;
                await Repo.SaveUserProfile(profile);

                var updated = await Repo.GetUserProfile();
                EventBus.EmitCoinsChanged(updated?.Coins ?? 0);

                Debug.Log("[DiaryController] Recompensa diaria de diario: +5 monedas");

                EventBus.EmitRewardGranted(new RewardGrant
                {
                    Source = RewardSource.Diary,
                    Coins  = 5,
                    Title  = "Has escrito en tu diario",
                    Body   = "+5 monedas"
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DiaryController] _grantDiaryReward: {ex.Message}");
            }
        }
    }
}
