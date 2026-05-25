using System;
using UnityEngine;
using Lutra.Core.Data.Models;
using Lutra.Core.Architecture;
using Lutra.Features.Charts;

namespace Lutra.Core.Events
{
    /// <summary>
    /// Canal de eventos estático que desacopla emisores y receptores.
    ///
    /// USO:
    ///   Suscribir  → EventBus.OnEmotionRegistered += _onEmotionRegistered;
    ///   Emitir     → EventBus.EmitEmotionRegistered(record);
    ///   Limpiar    → EventBus.OnEmotionRegistered -= _onEmotionRegistered; (en OnDisable/OnDestroy)
    ///
    /// Cada Emit* itera los suscriptores individualmente: una excepción en uno no cancela los demás.
    /// </summary>
    public static class EventBus
    {
        // ── Emociones ──────────────────────────────────────────────────

        public static event Action<EmotionRecord> OnEmotionRegistered;
        public static event Action<EmotionType>   OnCurrentEmotionChanged;

        // ── Racha ──────────────────────────────────────────────────────

        public static event Action<int> OnStreakUpdated;
        public static event Action      OnStreakBroken;

        // ── Minijuegos ─────────────────────────────────────────────────

        public static event Action<MinigameType>    OnMinigameStarted;
        public static event Action<MinigameSession> OnMinigameCompleted;

        // ── Navegación ─────────────────────────────────────────────────

        public static event Action<AppState> OnScreenChanged;

        // ── Diario ─────────────────────────────────────────────────────

        public static event Action<DiaryEntry> OnDiaryEntrySaved;

        // ── Economía ───────────────────────────────────────────────────

        public static event Action<int> OnCoinsChanged;

        // ── Informes ───────────────────────────────────────────────────

        public static event Action<ChartPeriod> OnChartPeriodChanged;

        // ── Ajustes ────────────────────────────────────────────────────

        public static event Action<AppSettings> OnSettingsChanged;

        // ── Recompensas ────────────────────────────────────────────────

        public static event Action<string, RewardType> OnRewardEarned;
        public static event Action<RewardDefinition>   OnRewardUnlocked;

        // ── Modal de emoción ───────────────────────────────────────────

        public static event Action OnEmotionModalRequested;

        // ── Métodos de emisión ─────────────────────────────────────────

        public static void EmitEmotionRegistered(EmotionRecord record)     => _safeInvoke(OnEmotionRegistered, record, nameof(OnEmotionRegistered));
        public static void EmitCurrentEmotionChanged(EmotionType emotion)  => _safeInvoke(OnCurrentEmotionChanged, emotion, nameof(OnCurrentEmotionChanged));
        public static void EmitStreakUpdated(int days)                      => _safeInvoke(OnStreakUpdated, days, nameof(OnStreakUpdated));
        public static void EmitStreakBroken()                               => _safeInvoke(OnStreakBroken, nameof(OnStreakBroken));
        public static void EmitMinigameStarted(MinigameType type)           => _safeInvoke(OnMinigameStarted, type, nameof(OnMinigameStarted));
        public static void EmitMinigameCompleted(MinigameSession session)   => _safeInvoke(OnMinigameCompleted, session, nameof(OnMinigameCompleted));
        public static void EmitScreenChanged(AppState state)                => _safeInvoke(OnScreenChanged, state, nameof(OnScreenChanged));
        public static void EmitDiaryEntrySaved(DiaryEntry entry)            => _safeInvoke(OnDiaryEntrySaved, entry, nameof(OnDiaryEntrySaved));
        public static void EmitCoinsChanged(int newTotal)                   => _safeInvoke(OnCoinsChanged, newTotal, nameof(OnCoinsChanged));
        public static void EmitChartPeriodChanged(ChartPeriod period)       => _safeInvoke(OnChartPeriodChanged, period, nameof(OnChartPeriodChanged));
        public static void EmitSettingsChanged(AppSettings settings)        => _safeInvoke(OnSettingsChanged, settings, nameof(OnSettingsChanged));
        public static void EmitRewardEarned(string itemId, RewardType type) => _safeInvoke(OnRewardEarned, itemId, type, nameof(OnRewardEarned));
        public static void EmitRewardUnlocked(RewardDefinition reward)      => _safeInvoke(OnRewardUnlocked, reward, nameof(OnRewardUnlocked));
        public static void EmitEmotionModalRequested()                       => _safeInvoke(OnEmotionModalRequested, nameof(OnEmotionModalRequested));

        // ── Limpieza ───────────────────────────────────────────────────

        public static void ClearAllListeners()
        {
            OnEmotionRegistered     = null;
            OnCurrentEmotionChanged = null;
            OnStreakUpdated         = null;
            OnStreakBroken          = null;
            OnMinigameStarted       = null;
            OnMinigameCompleted     = null;
            OnScreenChanged         = null;
            OnDiaryEntrySaved       = null;
            OnCoinsChanged          = null;
            OnChartPeriodChanged    = null;
            OnSettingsChanged       = null;
            OnRewardEarned          = null;
            OnRewardUnlocked        = null;
            OnEmotionModalRequested = null;
        }

        // ── Helpers de invocación segura ───────────────────────────────

        private static void _safeInvoke(Action evt, string name)
        {
            if (evt == null) return;
            foreach (Delegate d in evt.GetInvocationList())
                try   { ((Action)d)(); }
                catch (Exception ex) { Debug.LogError($"[EventBus] {name}: {ex.Message}"); }
        }

        private static void _safeInvoke<T>(Action<T> evt, T arg, string name)
        {
            if (evt == null) return;
            foreach (Delegate d in evt.GetInvocationList())
                try   { ((Action<T>)d)(arg); }
                catch (Exception ex) { Debug.LogError($"[EventBus] {name}: {ex.Message}"); }
        }

        private static void _safeInvoke<T1, T2>(Action<T1, T2> evt, T1 a1, T2 a2, string name)
        {
            if (evt == null) return;
            foreach (Delegate d in evt.GetInvocationList())
                try   { ((Action<T1, T2>)d)(a1, a2); }
                catch (Exception ex) { Debug.LogError($"[EventBus] {name}: {ex.Message}"); }
        }
    }
}
