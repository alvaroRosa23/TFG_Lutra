using System;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Persistence;

namespace Lutra.Features.SafeZone
{
    /// <summary>
    /// Gestiona la personalización visual del búho: color de plumas y accesorio activo.
    /// Las preferencias se persisten en UserProfile.Preferences con las claves
    /// "featherColorIndex" y "mascotAccessoryId".
    /// </summary>
    public class MascotCustomizer : MonoBehaviour
    {
        // ── Claves de preferencia ──────────────────────────────────────

        private const string PrefKeyColor     = "featherColorIndex";
        private const string PrefKeyAccessory = "mascotAccessoryId";

        // ── Referencias serializadas ───────────────────────────────────

        [SerializeField] private SpriteRenderer _mascotRenderer;
        [SerializeField] private Transform       _accessorySlot;

        [Tooltip("Items de tipo MascotAccessory disponibles para equipar.")]
        [SerializeField] private SafeZoneItem[] _accessoryItems;

        // ── Colores de plumas (12 predefinidos) ────────────────────────

        [SerializeField] private Color[] _featherColors = new Color[]
        {
            new Color(1.00f, 1.00f, 1.00f), // Blanco
            new Color(0.95f, 0.87f, 0.70f), // Crema
            new Color(0.87f, 0.72f, 0.53f), // Arena
            new Color(0.60f, 0.40f, 0.20f), // Marrón
            new Color(0.30f, 0.18f, 0.10f), // Marrón oscuro
            new Color(0.20f, 0.20f, 0.20f), // Carbón
            new Color(0.85f, 0.55f, 0.65f), // Rosa suave
            new Color(0.55f, 0.75f, 0.90f), // Azul cielo
            new Color(0.65f, 0.85f, 0.65f), // Verde menta
            new Color(0.90f, 0.75f, 0.40f), // Amarillo ocre
            new Color(0.70f, 0.55f, 0.85f), // Lavanda
            new Color(0.90f, 0.55f, 0.35f), // Naranja terracota
        };

        // ── Servicios ──────────────────────────────────────────────────

        private DataRepository _dataRepository;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_mascotRenderer == null)
                _mascotRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private async void Start()
        {
            _dataRepository = ServiceLocator.Get<DataRepository>();
            await LoadSavedCustomization();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Aplica el color de plumas del índice indicado y lo persiste en el perfil.
        /// </summary>
        public void ApplyColor(int colorIndex)
            => _ = _safeApplyColor(colorIndex);

        private async Task _safeApplyColor(int colorIndex)
        {
            try
            {
                if (colorIndex < 0 || colorIndex >= _featherColors.Length) return;
                if (_mascotRenderer != null)
                    _mascotRenderer.color = _featherColors[colorIndex];
                await _savePreference(PrefKeyColor, colorIndex.ToString());
            }
            catch (Exception ex)
            { Debug.LogError($"[MascotCustomizer] {ex.Message}"); }
        }

        /// <summary>
        /// Equipa el accesorio indicado por itemId en el _accessorySlot.
        /// Destruye el accesorio anterior. Persiste la selección en el perfil.
        /// </summary>
        public async Task ApplyAccessory(string itemId)
        {
            // Destruir accesorio actual
            if (_accessorySlot != null)
                for (int i = _accessorySlot.childCount - 1; i >= 0; i--)
                    Destroy(_accessorySlot.GetChild(i).gameObject);

            // Buscar y colocar el nuevo
            SafeZoneItem accessory = _findAccessory(itemId);
            if (accessory?.prefab != null && _accessorySlot != null)
                Instantiate(accessory.prefab, _accessorySlot, false);

            await _savePreference(PrefKeyAccessory, itemId);
        }

        /// <summary>
        /// Lee las preferencias guardadas del UserProfile y aplica el color y accesorio.
        /// </summary>
        public async Task LoadSavedCustomization()
        {
            var profile = await _dataRepository.GetUserProfile();
            if (profile == null) return;

            var prefs = profile.Preferences;

            // Restaurar color de plumas
            if (prefs.TryGetValue(PrefKeyColor, out string colorStr)
                && int.TryParse(colorStr, out int colorIndex))
            {
                if (_featherColors != null && colorIndex >= 0 && colorIndex < _featherColors.Length)
                    if (_mascotRenderer != null)
                        _mascotRenderer.color = _featherColors[colorIndex];
            }

            // Restaurar accesorio
            if (prefs.TryGetValue(PrefKeyAccessory, out string accessoryId)
                && !string.IsNullOrEmpty(accessoryId))
            {
                SafeZoneItem accessory = _findAccessory(accessoryId);
                if (accessory?.prefab != null && _accessorySlot != null)
                    Instantiate(accessory.prefab, _accessorySlot, false);
            }
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>Actualiza una clave en UserProfile.Preferences y guarda.</summary>
        private async Task _savePreference(string key, string value)
        {
            var profile = await _dataRepository.GetUserProfile();
            if (profile == null) return;

            var prefs = profile.Preferences;
            prefs[key] = value;
            profile.Preferences = prefs;

            await _dataRepository.SaveUserProfile(profile);
        }

        private SafeZoneItem _findAccessory(string itemId)
        {
            if (_accessoryItems == null) return null;
            foreach (var item in _accessoryItems)
                if (item != null && item.itemId == itemId) return item;
            return null;
        }
    }
}
