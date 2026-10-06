using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Systems;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Reúne desde DataRepository todo lo que necesita ReportCalculator para un periodo y lo calcula.
    /// Punto de entrada único para la pantalla de Estadísticas y el informe profesional.
    /// </summary>
    public static class ReportDataLoader
    {
        private const string LexiconResource = "DiaryLexicon_es";

        private static DiaryLexicon _lexicon;

        /// <summary>Diccionario del diario (Assets/Resources/DiaryLexicon_es.txt), cargado una sola vez.</summary>
        public static DiaryLexicon Lexicon
        {
            get
            {
                if (_lexicon != null) return _lexicon;

                var asset = Resources.Load<TextAsset>(LexiconResource);
                if (asset == null)
                    Debug.LogWarning($"[ReportDataLoader] No se encontró Resources/{LexiconResource}: sin análisis de lenguaje.");
                _lexicon = DiaryLexicon.Parse(asset != null ? asset.text : string.Empty);
                return _lexicon;
            }
        }

        /// <summary>
        /// Calcula el informe del periodo [from, to]. El periodo anterior para comparar tiene la misma
        /// duración y termina justo antes de <paramref name="from"/>.
        /// </summary>
        public static async Task<ReportData> LoadAsync(DateTime from, DateTime to)
        {
            var repo    = ServiceLocator.Get<DataRepository>();
            var profile = await repo.GetUserProfile();

            // "Todo": desde el alta del perfil
            if (from.Year < 2000 && profile != null && profile.CreationDate.Year >= 2000)
                from = profile.CreationDate.Date;

            TimeSpan length       = to - from;
            DateTime previousTo   = from.AddTicks(-1);
            DateTime previousFrom = from.Year >= 2000 ? from - length : from;

            var input = new ReportInput
            {
                From                 = from,
                To                   = to,
                Today                = DateTime.Today,
                ProfileCreatedAt     = profile?.CreationDate,
                Records              = await repo.GetEmotionsForPeriod(from, to),
                PreviousRecords      = from.Year >= 2000
                                       ? await repo.GetUserEmotionsForPeriod(previousFrom, previousTo)
                                       : new List<EmotionRecord>(),
                Sessions             = await repo.GetSessionsForPeriod(from, to),
                DiaryEntries         = await repo.GetDiaryEntriesForPeriod(from, to),
                Who5Responses        = await repo.GetScaleResponsesForPeriod(ScaleType.Who5, DateTime.MinValue, to),
                SupportNotifications = await repo.GetNotificationsOfTypeForPeriod(NotificationType.Support, from, to),
                DiaryAnalysisEnabled = ConsentGate.IsDiaryAnalysisEnabled(profile),
                Lexicon              = Lexicon
            };

            if (ServiceLocator.TryGet<StreakManager>(out var streaks))
            {
                input.CurrentStreak = await streaks.GetCurrentStreak();
                input.LongestStreak = await streaks.GetLongestStreak();
            }

            return ReportCalculator.Calculate(input);
        }
    }
}
