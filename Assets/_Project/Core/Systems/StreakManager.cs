using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Events;

namespace Lutra.Core.Systems
{
    /// <summary>
    /// Gestiona la racha de días consecutivos del usuario.
    /// Calcula la racha, detecta roturas y emite recompensas por hitos (7, 14, 30 días).
    ///
    /// Depende de DataRepository; lo resuelve bajo demanda desde ServiceLocator la primera
    /// vez que se necesita, sin requerir un orden de inicialización estricto.
    /// </summary>
    public class StreakManager : BaseService
    {
        // Hitos de racha que otorgan recompensa
        private static readonly int[] StreakMilestones = { 7, 14, 30 };

        private DataRepository _repository;

        /// <summary>Resuelve DataRepository bajo demanda la primera vez que se usa.</summary>
        private DataRepository Repository
        {
            get
            {
                if (_repository == null)
                    _repository = ServiceLocator.Get<DataRepository>();
                return _repository;
            }
        }

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake() { }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Devuelve la racha actual de días consecutivos.</summary>
        public async Task<int> GetCurrentStreak()
        {
            return await Repository.GetCurrentStreak();
        }

        /// <summary>Devuelve la racha más larga registrada históricamente.</summary>
        public async Task<int> GetLongestStreak()
        {
            return await Repository.GetLongestStreak();
        }

        /// <summary>
        /// Devuelve true si el usuario ya ha hecho check-in emocional hoy.
        /// Evita duplicar registros si el usuario abre la app varias veces al día.
        /// </summary>
        public async Task<bool> HasCheckedInToday()
        {
            var todayRecord = await Repository.GetTodayEmotion();
            return todayRecord != null;
        }

        /// <summary>
        /// Registra un check-in emocional, calcula la nueva racha y emite los eventos correspondientes.
        /// - EmitStreakUpdated con la racha actual.
        /// - EmitStreakBroken si el usuario no registró emoción ayer.
        /// - EmitRewardEarned en hitos de 7, 14 y 30 días.
        /// </summary>
        public async Task RegisterCheckIn(EmotionRecord record)
        {
            // Capturar racha previa antes de guardar el registro
            int previousStreak       = await Repository.GetCurrentStreak();
            bool hadPreviousActivity = previousStreak > 0;

            // Persistir el registro emocional
            await Repository.SaveEmotion(record);

            // Calcular nueva racha
            int newStreak = await Repository.GetCurrentStreak();

            // Detectar rotura de racha: había racha y ahora empieza desde 1
            if (hadPreviousActivity && newStreak == 1 && previousStreak > 1)
            {
                Debug.Log($"[StreakManager] Racha rota. Era {previousStreak} días.");
                EventBus.EmitStreakBroken();
            }

            // Notificar racha actualizada
            EventBus.EmitStreakUpdated(newStreak);
            Debug.Log($"[StreakManager] Racha actual: {newStreak} días.");

            // Recompensa de monedas por check-in: hitos dan más monedas
            int coins = newStreak == 7  ? 20
                      : newStreak == 14 ? 50
                      : newStreak == 30 ? 100
                      : 5;

            await Repository.AddCoins(coins);
            var profile = await Repository.GetUserProfile();
            EventBus.EmitCoinsChanged(profile?.Coins ?? 0);

            Debug.Log($"[StreakManager] Monedas ganadas por check-in: {coins}");

            // Comprobar hitos y emitir recompensas de decoración
            _checkMilestoneRewards(newStreak);
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Comprueba si la racha actual coincide con algún hito y emite la recompensa correspondiente.
        /// </summary>
        private void _checkMilestoneRewards(int streak)
        {
            foreach (int milestone in StreakMilestones)
            {
                if (streak == milestone)
                {
                    string itemId         = $"streak_reward_{milestone}d";
                    RewardType rewardType = RewardType.RoomDecoration;

                    Debug.Log($"[StreakManager] ¡Hito de racha alcanzado! {milestone} días → {itemId}");
                    EventBus.EmitRewardEarned(itemId, rewardType);
                    break;
                }
            }
        }
    }
}
