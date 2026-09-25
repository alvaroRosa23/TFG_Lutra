using System;
using UnityEngine;

namespace Lutra.Minigames
{
    /// <summary>
    /// Parámetros de ajuste de BreathJump. El salto se diseña a partir del ritmo 4-6:
    /// inspirar 4 s carga el salto completo y el vuelo dura algo menos que la espiración
    /// (airTimeSeconds), de modo que el jugador aterriza, espira el resto en la plataforma
    /// y vuelve a inspirar justo cuando el círculo guía termina de contraerse.
    /// </summary>
    [Serializable]
    public class BreathJumpTuning
    {
        [Header("Ritmo de respiración (segundos)")]
        public float inhaleSeconds = 4f;
        public float exhaleSeconds = 6f;

        [Tooltip("Margen (±s) para que la inspiración/espiración cuenten como perfectas")]
        public float perfectInhaleTolerance = 0.6f;
        public float perfectExhaleTolerance = 0.8f;

        [Tooltip("Margen (±s) para que cuenten como buenas")]
        public float goodInhaleTolerance = 1.5f;
        public float goodExhaleTolerance = 2f;

        [Tooltip("Pulsaciones más cortas se ignoran (evita saltos por toques accidentales)")]
        public float minInhaleToJump = 1f;

        [Header("Partida")]
        [Tooltip("Respiraciones (saltos) hasta la meta")]
        public int breathsToComplete = 20;

        [Tooltip("Plataformas que se retrocede al caer: 0 = la plataforma desde la que se saltó")]
        public int respawnPlatformsBack = 0;

        [Header("Salto")]
        [Tooltip("Duración del vuelo con carga completa; el resto de la espiración se hace en la plataforma")]
        public float airTimeSeconds = 4.5f;
        [Tooltip("Altura del arco (unidades de mundo) en un salto entre plataformas a la misma altura")]
        public float arcHeight = 2.5f;
        [Tooltip("Margen horizontal extra para aterrizar en el borde de una plataforma")]
        public float landingEdgeGrace = 0.2f;
        public float fallGravityMultiplier = 3f;
        [Tooltip("Distancia por debajo de la plataforma objetivo a la que se reaparece")]
        public float fallDepthToRespawn = 4f;

        [Header("Corrección en el aire (mantener pulsado)")]
        [Tooltip("Aceleración horizontal (u/s²) hacia el centro de la plataforma objetivo")]
        public float steerAcceleration = 3f;
        [Tooltip("Multiplicador de gravedad al bajar mientras se corrige (planeo)")]
        public float glideGravityMultiplier = 0.5f;

        [Header("Plataformas (distancias entre centros, en unidades de mundo)")]
        public float   startPlatformWidth = 3f;
        public float   goalPlatformWidth  = 5f;
        public Vector2 gapRange    = new Vector2(3.6f, 4.6f);
        public Vector2 riseRange   = new Vector2(-0.3f, 1f);   // tendencia ascendente
        public Vector2 widthRange  = new Vector2(2.2f, 2.8f);
        public float   minEdgeGap  = 0.8f;                      // hueco mínimo visible entre bordes

        /// <summary>Gravedad derivada para que el arco tenga arcHeight y dure airTimeSeconds.</summary>
        public float Gravity => 8f * arcHeight / (airTimeSeconds * airTimeSeconds);
    }
}
