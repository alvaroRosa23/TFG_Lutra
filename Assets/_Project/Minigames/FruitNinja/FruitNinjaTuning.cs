using System;
using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Minigames
{
    /// <summary>
    /// Parámetros de ajuste de FruitNinja. Las distancias están en unidades del Canvas
    /// (referencia 1080×1920). Los valores de cada oleada se interpolan entre los de Descarga
    /// y los de Calma según avanza la Transición.
    /// </summary>
    [Serializable]
    public class FruitNinjaTuning
    {
        [Header("Partida (segundos)")]
        public float durationSeconds   = 120f;
        [Tooltip("Fin de la fase de Descarga (empieza la Transición)")]
        public float releaseEndSeconds = 45f;
        [Tooltip("Inicio de la fase de Calma (termina la Transición)")]
        public float calmStartSeconds  = 80f;
        public float firstWaveDelay    = 1f;
        [Tooltip("Espera máxima a que caigan los últimos elementos tras acabar el tiempo")]
        public float endGraceSeconds   = 6f;

        [Header("Oleadas en Descarga")]
        public Vector2    releaseWaveInterval = new Vector2(0.8f, 1.2f);
        public Vector2Int releaseWaveSize     = new Vector2Int(2, 5);
        public float      releaseFlightTime   = 2f;
        public float      releaseSizeScale    = 1f;

        [Header("Oleadas en Calma")]
        public Vector2    calmWaveInterval = new Vector2(2f, 2.6f);
        public Vector2Int calmWaveSize     = new Vector2Int(1, 2);
        public float      calmFlightTime   = 3.4f;
        public float      calmSizeScale    = 1.35f;

        [Header("Trayectorias")]
        public float   baseDiameter       = 170f;
        [Tooltip("Altura máxima (fracción de la pantalla desde abajo) de los que salen desde abajo")]
        public Vector2 apexHeightRange    = new Vector2(0.55f, 0.82f);
        [Tooltip("Altura máxima de los que entran por los laterales")]
        public Vector2 sideApexRange      = new Vector2(0.5f, 0.75f);
        [Tooltip("Fracción del ancho en la que nacen los elementos")]
        public float   spawnWidthFraction = 0.7f;
        [Tooltip("Segundos entre los elementos de una misma oleada")]
        public float   waveStagger        = 0.12f;
        public float   maxSpin            = 200f;

        [Header("Corte")]
        [Tooltip("Velocidad mínima del dedo (unidades/s) para que el trazo corte")]
        public float minSwipeSpeed    = 900f;
        [Tooltip("Radio de corte respecto al radio visual")]
        public float cutRadiusScale   = 0.9f;
        [Tooltip("Segundos sin cortar que cierran un combo (también se cierra al levantar el dedo)")]
        public float comboWindow      = 0.35f;

        [Header("Combo grande (zoom)")]
        [Tooltip("Elementos en un mismo combo para el zoom (más de 5)")]
        public int   bigComboThreshold = 6;
        public float bigComboCooldown  = 2.5f;
        [Tooltip("Congelado breve del mundo al hacer zoom")]
        public float hitStopSeconds    = 0.15f;
        [Range(0f, 1f)] public float hitStopTimeScale = 0.1f;

        [Header("Especiales")]
        [Tooltip("Probabilidad por oleada en Descarga (baja a 0 al llegar a Calma)")]
        [Range(0f, 1f)] public float specialChancePerWave = 0.15f;
        public float minSecondsBetweenSpecials = 7f;
        public SliceableKind[] enabledSpecials =
        {
            SliceableKind.Burst, SliceableKind.Rain, SliceableKind.Crossfire, SliceableKind.SlowMotion
        };

        public int   burstCount         = 6;
        public float rainSeconds        = 3f;
        public float rainInterval       = 0.25f;
        public float crossfireSeconds   = 4f;
        public float crossfireInterval  = 0.45f;
        public float slowMotionSeconds  = 3f;
        [Range(0.1f, 1f)] public float slowMotionScale = 0.5f;

        [Header("Puntuación")]
        [Tooltip("Puntos (sobre 100) que resta cada elemento que se cae")]
        public float missPenaltyPoints = 1f;

        /// <summary>Deja de lanzar cuando queda menos que esto, para que todo caiga antes del final.</summary>
        public float LastSpawnMargin => Mathf.Max(releaseFlightTime, calmFlightTime) + 0.5f;
    }
}
