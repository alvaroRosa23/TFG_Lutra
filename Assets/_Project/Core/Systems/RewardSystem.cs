using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Events;

namespace Lutra.Core.Systems
{
    /// <summary>
    /// Evalúa y otorga recompensas cada vez que el usuario registra una emoción.
    /// Comprueba condiciones de racha y de check-ins totales contra las
    /// RewardDefinitions configuradas en el inspector.
    /// </summary>
    public class RewardSystem : BaseService
    {
        [SerializeField] private RewardDefinition[] _allRewards;

        // ── Servicios ──────────────────────────────────────────────────

        private DataRepository _dataRepository;
        private StreakManager  _streakManager;

        private DataRepository Repository           => _dataRepository ??= ServiceLocator.Get<DataRepository>();
        private StreakManager  StreakManagerService => _streakManager  ??= ServiceLocator.Get<StreakManager>();

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake() { }

        private void OnEnable()
        {
            EventBus.OnEmotionRegistered += _onEmotionRegistered;
        }

        private void OnDisable()
        {
            EventBus.OnEmotionRegistered -= _onEmotionRegistered;
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Punto de entrada principal. Evalúa todas las condiciones de recompensa
        /// y otorga las que corresponden al estado actual del usuario.
        /// </summary>
        public async Task CheckAndGrantRewards(EmotionRecord newRecord)
        {
            try
            {
                var profile = await Repository.GetUserProfile();
                if (profile == null)
                {
                    Debug.LogWarning("[RewardSystem] CheckAndGrantRewards: no existe perfil de usuario.");
                    return;
                }

                // Cargar ítems desbloqueados desde la tabla normalizada
                profile.UnlockedItems = await Repository.GetUnlockedItemIds(profile.Id);

                int streak = await StreakManagerService.GetCurrentStreak();

                await _checkStreakRewards(profile, streak);
                await _checkCheckInRewards(profile);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RewardSystem] CheckAndGrantRewards: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Devuelve las recompensas que el usuario aún no ha desbloqueado
        /// (o todas las repetibles independientemente del estado).
        /// </summary>
        public async Task<List<RewardDefinition>> GetAvailableRewards()
        {
            try
            {
                var profile = await Repository.GetUserProfile();
                var available = new List<RewardDefinition>();

                if (_allRewards == null) return available;

                foreach (var reward in _allRewards)
                {
                    if (reward == null) continue;
                    if (!reward.isRepeatable && profile != null && profile.UnlockedItems.Contains(reward.rewardId))
                        continue;
                    available.Add(reward);
                }

                return available;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RewardSystem] GetAvailableRewards: {ex.Message}\n{ex.StackTrace}");
                return new List<RewardDefinition>();
            }
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Otorga recompensas cuyo requiredStreakDays coincide con la racha actual.
        /// Modifica el perfil en memoria; la persistencia la hace CheckAndGrantRewards.
        /// </summary>
        private async Task _checkStreakRewards(UserProfile profile, int streak)
        {
            if (_allRewards == null) return;

            foreach (var reward in _allRewards)
            {
                if (reward == null || reward.requiredStreakDays <= 0) continue;
                if (reward.requiredStreakDays != streak) continue;

                // Saltar si ya fue otorgada y no es repetible
                if (!reward.isRepeatable && profile.UnlockedItems.Contains(reward.rewardId)) continue;

                await _grantReward(reward, profile);
            }
        }

        /// <summary>
        /// Otorga recompensas cuyo requiredCheckIns coincide con el total de check-ins del usuario.
        /// Modifica el perfil en memoria; la persistencia la hace CheckAndGrantRewards.
        /// </summary>
        private async Task _checkCheckInRewards(UserProfile profile)
        {
            if (_allRewards == null) return;

            int totalCheckIns = await Repository.GetTotalCheckIns();

            foreach (var reward in _allRewards)
            {
                if (reward == null || reward.requiredCheckIns <= 0) continue;
                if (reward.requiredCheckIns != totalCheckIns) continue;

                // Saltar si ya fue otorgada y no es repetible
                if (!reward.isRepeatable && profile.UnlockedItems.Contains(reward.rewardId)) continue;

                await _grantReward(reward, profile);
            }
        }

        /// <summary>
        /// Aplica una recompensa concreta: añade monedas y/o desbloquea ítem,
        /// marca el rewardId como obtenido y emite los eventos correspondientes.
        /// </summary>
        private async Task _grantReward(RewardDefinition reward, UserProfile profile)
        {
            // Entregar monedas
            if (reward.coinValue > 0)
            {
                await Repository.AddCoins(reward.coinValue);
                var updatedProfile = await Repository.GetUserProfile();
                EventBus.EmitCoinsChanged(updatedProfile?.Coins ?? 0);
            }

            // Desbloquear ítem asociado en la tabla normalizada
            if (!string.IsNullOrEmpty(reward.itemId))
                await Repository.UnlockItem(profile.Id, reward.itemId);

            // Marcar el rewardId como obtenido para evitar duplicados
            await Repository.UnlockItem(profile.Id, reward.rewardId);

            // Actualizar la lista en memoria para que los checks del mismo ciclo sean correctos
            if (!profile.UnlockedItems.Contains(reward.itemId ?? ""))
                profile.UnlockedItems.Add(reward.itemId ?? "");
            if (!profile.UnlockedItems.Contains(reward.rewardId))
                profile.UnlockedItems.Add(reward.rewardId);

            EventBus.EmitRewardEarned(reward.rewardId, reward.type);
            EventBus.EmitRewardUnlocked(reward);

            Debug.Log($"[RewardSystem] Recompensa otorgada: {reward.displayName} ({reward.rewardId})");
        }

        private void _onEmotionRegistered(EmotionRecord record)
            => _ = _onEmotionRegisteredAsync(record);

        private async Task _onEmotionRegisteredAsync(EmotionRecord record)
        {
            try { await CheckAndGrantRewards(record); }
            catch (Exception ex)
            { Debug.LogError($"[RewardSystem] {ex.Message}"); }
        }
    }
}
