using System;
using System.Collections.Generic;
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
    /// Gestiona la carga de entradas, el editor y la búsqueda.
    /// </summary>
    public class DiaryController : MonoBehaviour
    {
        [SerializeField] private DiaryView _view;

        // ── Servicios ──────────────────────────────────────────────────

        private DataRepository _dataRepository;
        private DataRepository Repo => _dataRepository ??= ServiceLocator.Get<DataRepository>();

        private AuthManager _authManager;
        private AuthManager Auth => _authManager ??= ServiceLocator.Get<AuthManager>();

        private FirestoreManager _firestoreManager;
        private FirestoreManager Firestore => _firestoreManager ??= ServiceLocator.Get<FirestoreManager>();

        // ── Estado ─────────────────────────────────────────────────────

        private List<DiaryEntry> _currentMonthEntries = new();
        private DateTime         _currentMonth;
        private DiaryEntry       _editingEntry;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null)
                _view = GetComponentInChildren<DiaryView>();

            _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        }

        private void OnEnable()  => _subscribeToView();
        private void OnDisable() => _unsubscribeFromView();

        // ── API pública ────────────────────────────────────────────────

        public async Task OpenDiary()
        {
            try
            {
                _view?.ShowMainView();

                _currentMonthEntries = await Repo.GetDiaryEntriesForMonth(
                    _currentMonth.Year, _currentMonth.Month);

                _view?.RefreshEntries(_currentMonthEntries);
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

                _editingEntry = entry;
                _view?.ShowEditorView(entry);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DiaryController] OpenNewEntry: {ex.Message}\n{ex.StackTrace}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        public Task OpenEntry(DiaryEntry entry)
        {
            _editingEntry = entry;
            _view?.ShowEditorView(entry);
            return Task.CompletedTask;
        }

        public async Task SaveEntry(DiaryEntry entry)
        {
            try
            {
                entry.Title   = _view?.GetTitle()   ?? string.Empty;
                entry.Content = _view?.GetContent() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(entry.Content))
                {
                    _view?.ShowError("Escribe algo en tu entrada");
                    return;
                }

                await Repo.SaveDiaryEntry(entry);
                _ = _syncEntryToFirestore(entry);
                _ = _grantDiaryReward();

                _currentMonthEntries = await Repo.GetDiaryEntriesForMonth(
                    _currentMonth.Year, _currentMonth.Month);

                _view?.ShowMainView();
                _view?.RefreshEntries(_currentMonthEntries);

                EventBus.EmitDiaryEntrySaved(entry);
                _editingEntry = null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DiaryController] SaveEntry: {ex.Message}\n{ex.StackTrace}");
                _view?.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        public async Task LoadEntriesForMonth(int year, int month)
        {
            try
            {
                _currentMonth        = new DateTime(year, month, 1);
                _currentMonthEntries = await Repo.GetDiaryEntriesForMonth(year, month);
                _view?.RefreshEntries(_currentMonthEntries);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DiaryController] LoadEntriesForMonth: {ex.Message}\n{ex.StackTrace}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        public async Task<DiaryEntry> GetEntryForDate(DateTime date)
        {
            try
            {
                if (date.Year == _currentMonth.Year && date.Month == _currentMonth.Month)
                {
                    var cached = _currentMonthEntries.FirstOrDefault(e => e.Date.Date == date.Date);
                    if (cached != null) return cached;
                }

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

            _view.OnNewEntryClicked  += _onNewEntryClicked;
            _view.OnSaveClicked      += _onSaveClicked;
            _view.OnBackClicked      += _onBackClicked;
            _view.OnSearchChanged    += _onSearchChanged;
            _view.OnEntryCardClicked += _onEntryCardClicked;
        }

        private void _unsubscribeFromView()
        {
            if (_view == null) return;

            _view.OnNewEntryClicked  -= _onNewEntryClicked;
            _view.OnSaveClicked      -= _onSaveClicked;
            _view.OnBackClicked      -= _onBackClicked;
            _view.OnSearchChanged    -= _onSearchChanged;
            _view.OnEntryCardClicked -= _onEntryCardClicked;
        }

        // ── Handlers de vista ──────────────────────────────────────────

        private void _onNewEntryClicked()      => _ = _safeOpenNewEntry();
        private void _onSaveClicked()          => _ = _safeSaveEntry();
        private void _onEntryCardClicked(DiaryEntry entry) => _ = _safeOpenEntry(entry);

        private void _onBackClicked()
        {
            _editingEntry = null;
            _view?.ShowMainView();
        }

        private void _onSearchChanged(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _view?.RefreshEntries(_currentMonthEntries);
                return;
            }

            string lower   = query.ToLowerInvariant();
            var    filtered = _currentMonthEntries
                .Where(e =>
                    (e.Title   != null && e.Title.ToLowerInvariant().Contains(lower)) ||
                    (e.Content != null && e.Content.ToLowerInvariant().Contains(lower)))
                .ToList();

            _view?.RefreshEntries(filtered);
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

        private async Task _syncEntryToFirestore(DiaryEntry entry)
        {
            try
            {
                if (!Auth.IsLoggedIn) return;
                await Firestore.SaveDiaryEntry(Auth.CurrentUserId, entry);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DiaryController] Sync a Firestore fallido: {ex.Message}");
            }
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
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DiaryController] _grantDiaryReward: {ex.Message}");
            }
        }
    }
}
