using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Events;
using Lutra.Core.Systems;
using Lutra.UI.Components;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Períodos de visualización disponibles en la sección de Informes.
    /// </summary>
    public enum ChartPeriod
    {
        Week,
        Month,
        AllTime
    }

    /// <summary>
    /// Controlador de la sección de Informes.
    /// Carga, calcula y sirve datos a ChartsView para los distintos períodos.
    /// </summary>
    public class ChartsController : MonoBehaviour
    {
        [SerializeField] private ChartsView _view;

        // ── Servicios ──────────────────────────────────────────────────

        private DataRepository _dataRepository;
        private StreakManager  _streakManager;

        // ── Estado ─────────────────────────────────────────────────────

        private ChartPeriod _currentPeriod = ChartPeriod.Week;
        private ChartsData  _cachedData;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null)
                _view = GetComponentInChildren<ChartsView>();
        }

        private void OnEnable()
        {
            _dataRepository = ServiceLocator.Get<DataRepository>();
            _streakManager  = ServiceLocator.Get<StreakManager>();
            _subscribeToView();
        }

        private void OnDisable()
        {
            _unsubscribeFromView();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Abre los informes con el período actualmente seleccionado.</summary>
        public async Task OpenCharts()
        {
            await LoadDataForPeriod(_currentPeriod);
        }

        /// <summary>
        /// Carga y calcula datos para el período indicado, refresca la vista y
        /// emite el evento de cambio de período.
        /// </summary>
        public async Task LoadDataForPeriod(ChartPeriod period)
        {
            try
            {
                _currentPeriod = period;

                (DateTime from, DateTime to) = _getDateRange(period);

                // Cargar registros emocionales del período
                var records = await _dataRepository.GetEmotionsForPeriod(from, to);

                // Cargar sesiones de todos los minijuegos del período
                var allSessions = new List<MinigameSession>();
                foreach (MinigameType type in Enum.GetValues(typeof(MinigameType)))
                {
                    var sessions = await _dataRepository.GetSessionsForMinigame(type);
                    // Filtrar por período
                    foreach (var s in sessions)
                        if (s.StartTime >= from && s.StartTime <= to)
                            allSessions.Add(s);
                }

                // Calcular métricas
                _cachedData = ChartsCalculator.Calculate(records, allSessions);

                // Enriquecer con datos de racha
                _cachedData.CurrentStreak = await _streakManager.GetCurrentStreak();
                _cachedData.LongestStreak = await _streakManager.GetLongestStreak();

                _view?.RefreshAll(_cachedData, period);

                EventBus.EmitChartPeriodChanged(period);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChartsController] LoadDataForPeriod: {ex.Message}\n{ex.StackTrace}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        /// <summary>
        /// Genera el informe semanal textual si es lunes y hay datos de la semana anterior.
        /// Devuelve string vacío si no procede generarlo.
        /// </summary>
        public async Task<string> GenerateWeeklyReport()
        {
            try
            {
                // El informe se genera los lunes
                if (DateTime.Now.DayOfWeek != DayOfWeek.Monday)
                    return string.Empty;

                var from = DateTime.Now.Date.AddDays(-7);
                var to   = DateTime.Now.Date.AddDays(-1);
                var records = await _dataRepository.GetEmotionsForPeriod(from, to);

                if (records == null || records.Count == 0)
                    return string.Empty;

                var allSessions = new List<MinigameSession>();
                foreach (MinigameType type in Enum.GetValues(typeof(MinigameType)))
                {
                    var sessions = await _dataRepository.GetSessionsForMinigame(type);
                    foreach (var s in sessions)
                        if (s.StartTime >= from && s.StartTime <= to)
                            allSessions.Add(s);
                }

                var weekData = ChartsCalculator.Calculate(records, allSessions);
                weekData.CurrentStreak = await _streakManager.GetCurrentStreak();

                string dominant  = _emotionDisplayName(weekData.MostFrequentEmotion);
                string minigame  = _minigameDisplayName(weekData.MostBeneficialMinigame);
                string streak    = ChartsCalculator.FormatStreak(weekData.CurrentStreak);

                string report =
                    $"Esta semana tu emoción dominante fue {dominant}. " +
                    $"El minijuego más beneficioso fue {minigame}. " +
                    $"Llevas {streak} de racha. ¡Sigue así!";

                _view?.ShowWeeklyReport(report);
                return report;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChartsController] GenerateWeeklyReport: {ex.Message}\n{ex.StackTrace}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
                return string.Empty;
            }
        }

        /// <summary>Cambia el período y recarga los datos.</summary>
        public void SetPeriod(ChartPeriod period)
        {
            _ = LoadDataForPeriod(period);
        }

        // ── Métodos privados ───────────────────────────────────────────

        private (DateTime from, DateTime to) _getDateRange(ChartPeriod period)
        {
            var now = DateTime.Now;
            return period switch
            {
                ChartPeriod.Week    => (now.Date.AddDays(-6), now),
                ChartPeriod.Month   => (new DateTime(now.Year, now.Month, 1), now),
                ChartPeriod.AllTime => (DateTime.MinValue, now),
                _                   => (now.Date.AddDays(-6), now)
            };
        }

        private static string _emotionDisplayName(EmotionType emotion) => emotion switch
        {
            EmotionType.Joy         => "Alegría",
            EmotionType.Calm        => "Calma",
            EmotionType.Sadness     => "Tristeza",
            EmotionType.Anxiety     => "Ansiedad",
            EmotionType.Frustration => "Frustración",
            EmotionType.Overwhelm   => "Agobio",
            EmotionType.Nostalgia   => "Nostalgia",
            EmotionType.Energy      => "Energía",
            _                       => emotion.ToString()
        };

        private static string _minigameDisplayName(MinigameType type) => type switch
        {
            MinigameType.Unpacking  => "Unpacking",
            MinigameType.FruitNinja => "Fruit Ninja",
            MinigameType.Beatmaker  => "Beatmaker",
            MinigameType.SandCastle => "Castillo de Arena",
            MinigameType.FluidSim   => "Fluid Simulator",
            MinigameType.BreathJump => "Breath Jump",
            MinigameType.Puzzle     => "Puzzle",
            MinigameType.StarFisher => "Pescador de Estrellas",
            _                       => type.ToString()
        };

        private void _subscribeToView()
        {
            if (_view == null) return;
            _view.OnPeriodChanged += SetPeriod;
        }

        private void _unsubscribeFromView()
        {
            if (_view == null) return;
            _view.OnPeriodChanged -= SetPeriod;
        }
    }
}
