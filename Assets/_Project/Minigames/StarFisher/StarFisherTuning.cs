using System;
using UnityEngine;

namespace Lutra.Minigames
{
    /// <summary>
    /// Parámetros de ajuste de StarFisher. Distancias en unidades del Canvas (referencia 1080×1920).
    /// Las tablas por rareza van en orden Común, Poco común, Rara, Épica, Legendaria.
    /// </summary>
    [Serializable]
    public class StarFisherTuning
    {
        [Header("Partida")]
        public int   casts             = 5;
        [Tooltip("Mensaje final antes de ir a PostMinigameScreen")]
        public float endMessageSeconds = 2.2f;

        [Header("Lanzamiento (barra de fuerza que oscila)")]
        [Tooltip("Segundos de una ida y vuelta completa de la barra (0 → 1 → 0)")]
        public float   powerCycleSeconds = 1.4f;
        [Tooltip("Tramo de la barra que cuenta como lanzamiento clavado")]
        public Vector2 perfectZone       = new Vector2(0.88f, 1f);
        [Tooltip("Fuerza mínima: aunque se suelte en 0 el anzuelo llega hasta aquí")]
        [Range(0f, 1f)] public float minPower = 0.15f;
        public float   castFlightSeconds = 0.9f;
        [Tooltip("Altura extra del arco del anzuelo en el vuelo")]
        public float   castArcHeight     = 260f;

        [Header("Espera")]
        public Vector2 waitSeconds     = new Vector2(5f, 10f);
        [Tooltip("Velocidad máxima del anzuelo al seguir el dedo (lento a propósito)")]
        public float   hookFollowSpeed = 170f;

        [Header("Zonas brillantes")]
        public int     maxGlowZones          = 2;
        public Vector2 glowZoneSpawnInterval = new Vector2(1f, 2.5f);
        public Vector2 glowZoneLifetime      = new Vector2(3.5f, 6f);
        public float   glowZoneRadius        = 130f;
        [Tooltip("Segundos dentro de zonas brillantes para la bonificación máxima")]
        public float   glowSecondsForMaxBonus = 3f;

        [Header("Picada")]
        public float biteWindowSeconds = 2f;
        public bool  vibrateOnBite     = true;

        [Header("Recogida")]
        [Tooltip("Toques necesarios según la rareza")]
        public int[] tapsByRarity          = { 5, 7, 9, 12, 15 };
        [Tooltip("Segundos sin tocar para que la estrella se escape")]
        public float reelIdleEscapeSeconds = 1.5f;
        [Tooltip("Zoom que se suma cada vez que se supera el umbral de una rareza")]
        public float zoomPerTier           = 0.07f;

        [Header("Rareza")]
        [Tooltip("Probabilidad base (pesos) de cada rareza")]
        public float[] rarityWeights       = { 60f, 25f, 10f, 4f, 1f };
        [Tooltip("Bonificación máxima por distancia (lanzamiento a tope)")]
        public float   distanceBonus       = 0.3f;
        public float   perfectCastBonus    = 0.4f;
        [Tooltip("Bonificación máxima por tiempo en zonas brillantes")]
        public float   glowBonus           = 0.5f;
        [Tooltip("Cuánto multiplica la bonificación cada escalón de rareza: peso × (1 + bonificación × escalón × fuerza)")]
        public float   rarityBoostStrength = 0.5f;
        [Tooltip("Peso extra de las estrellas aún no descubiertas dentro de su rareza")]
        public float   undiscoveredWeight  = 2f;

        [Header("Monedas")]
        public int[] coinsByRarity = { 1, 1, 3, 6, 6 };
        public int   maxCoins      = 10;

        [Header("Estrella de racha")]
        [Tooltip("Racha (días) con la que sale la estrella especial en el primer lanzamiento del día")]
        public int streakStarDays = 5;

        [Header("Captura y liberación")]
        public float flashInSeconds       = 0.25f;
        public float flashHoldSeconds     = 1.2f;
        public float flashOutSeconds      = 0.5f;
        public float escapeMessageSeconds = 2f;

        public int GetTaps(int rarity)  => _at(tapsByRarity, rarity, 5);
        public int GetCoins(int rarity) => _at(coinsByRarity, rarity, 1);

        private static int _at(int[] table, int index, int fallback)
        {
            if (table == null || table.Length == 0) return fallback;
            return table[Mathf.Clamp(index, 0, table.Length - 1)];
        }
    }
}
