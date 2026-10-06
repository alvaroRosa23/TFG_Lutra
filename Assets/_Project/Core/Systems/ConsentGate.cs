using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;

namespace Lutra.Core.Systems
{
    /// <summary>
    /// Consentimiento de tratamiento de datos de salud (RGPD art. 9, docs/PROFESSIONAL_REPORT.md §6.2).
    ///
    /// Se pide después de crear el perfil y antes del primer check-in (nombre y fecha de nacimiento
    /// no son datos de salud; los registros emocionales sí). También a los usuarios existentes sin
    /// consentimiento y cuando cambia la versión del texto. Se guarda en UserProfile.Preferences, así
    /// que se sincroniza con Firestore como el resto de preferencias.
    ///
    /// Uso: en vez de TransitionTo(MainMenu / EmotionCheck) tras el login, el arranque o el
    /// onboarding, llamar a ContinueTo(destino).
    /// </summary>
    public static class ConsentGate
    {
        /// <summary>Subir la versión al cambiar el texto del consentimiento: se volverá a pedir.</summary>
        public const string CurrentVersion = "1.0";

        public const string VersionKey      = "consentVersion";
        public const string DateKey         = "consentDate";
        public const string DiaryAnalysisKey = "diaryLanguageAnalysis";

        /// <summary>Destino al que ir tras aceptar (lo usa ConsentController).</summary>
        public static AppState PendingTarget { get; private set; } = AppState.EmotionCheck;

        // ── Consultas ──────────────────────────────────────────────────

        public static bool HasConsent(UserProfile profile)
        {
            var prefs = profile?.Preferences;
            return prefs != null && prefs.TryGetValue(VersionKey, out var version) && version == CurrentVersion;
        }

        /// <summary>Análisis de escritura del diario: activado salvo que el usuario lo desactive.</summary>
        public static bool IsDiaryAnalysisEnabled(UserProfile profile)
        {
            var prefs = profile?.Preferences;
            return prefs == null || !prefs.TryGetValue(DiaryAnalysisKey, out var value) || value != "false";
        }

        // ── Navegación ─────────────────────────────────────────────────

        /// <summary>Va a <paramref name="target"/> si ya hay consentimiento; si no, pasa antes por la pantalla de consentimiento.</summary>
        public static async Task ContinueTo(AppState target)
        {
            bool hasConsent = true;
            try
            {
                var profile = await ServiceLocator.Get<DataRepository>().GetUserProfile();
                hasConsent = profile == null || HasConsent(profile);
            }
            catch (Exception ex)
            {
                // Sin poder leer el perfil no se bloquea la entrada; se volverá a comprobar en el siguiente arranque
                Debug.LogError($"[ConsentGate] ContinueTo: {ex.Message}");
            }

            if (hasConsent)
            {
                AppStateMachine.Instance.TransitionTo(target);
                return;
            }

            PendingTarget = target;
            AppStateMachine.Instance.TransitionTo(AppState.Consent);
        }

        // ── Escritura ──────────────────────────────────────────────────

        /// <summary>Guarda la aceptación (versión, fecha y análisis del diario) en las preferencias del perfil.</summary>
        public static Task SaveConsent(bool diaryLanguageAnalysis)
            => _updatePreferences(prefs =>
            {
                prefs[VersionKey]       = CurrentVersion;
                prefs[DateKey]          = DateTime.Now.ToString("o");
                prefs[DiaryAnalysisKey] = diaryLanguageAnalysis ? "true" : "false";
            });

        /// <summary>Activa o desactiva el análisis de escritura del diario (Ajustes).</summary>
        public static Task SetDiaryAnalysis(bool enabled)
            => _updatePreferences(prefs => prefs[DiaryAnalysisKey] = enabled ? "true" : "false");

        private static async Task _updatePreferences(Action<Dictionary<string, string>> change)
        {
            var repo    = ServiceLocator.Get<DataRepository>();
            var profile = await repo.GetUserProfile();
            if (profile == null) throw new InvalidOperationException("No hay perfil de usuario.");

            var prefs = profile.Preferences ?? new Dictionary<string, string>();
            change(prefs);
            profile.Preferences = prefs;
            await repo.SaveUserProfile(profile);
        }
    }
}
