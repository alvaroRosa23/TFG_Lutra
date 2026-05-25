using System;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;

namespace Lutra.Core.Data.Persistence
{
    /// <summary>
    /// Gestiona la conexión async a SQLite y la creación inicial de tablas.
    /// Usa SQLiteAsyncConnection para no bloquear el hilo principal de Unity.
    /// Se registra en el ServiceLocator durante el arranque (GameManager).
    /// </summary>
    public class DatabaseManager : IService
    {
        private const string DbFileName = "owlet.db";

        private SQLiteAsyncConnection _connection;

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Abre la conexión async y crea las tablas si no existen.
        /// Debe esperarse con await antes de usar cualquier otro método.
        /// </summary>
        public async Task Initialize()
        {
            try
            {
                string dbPath = Path.Combine(Application.persistentDataPath, DbFileName);
                _connection = new SQLiteAsyncConnection(dbPath);

                await _createTablesAsync();

                Debug.Log($"[DatabaseManager] Base de datos inicializada en: {dbPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DatabaseManager] Error al inicializar: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Devuelve la conexión async activa.
        /// Lanza excepción si se llama antes de Initialize().
        /// </summary>
        public SQLiteAsyncConnection GetConnection()
        {
            if (_connection == null)
                throw new InvalidOperationException(
                    "[DatabaseManager] Conexión no inicializada. Llama a Initialize() primero.");

            return _connection;
        }

        /// <summary>
        /// Cierra la conexión de forma segura al salir de la app.
        /// Accede a la conexión subyacente síncronamente desde OnDestroy.
        /// </summary>
        public void Close()
        {
            try
            {
                _connection?.GetConnection().Close();
                _connection = null;
                Debug.Log("[DatabaseManager] Conexión cerrada.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DatabaseManager] Error al cerrar la conexión: {ex.Message}");
            }
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Crea todas las tablas del esquema si aún no existen (operación idempotente).
        /// Añadir aquí cualquier modelo nuevo que necesite persistencia.
        /// </summary>
        private async Task _createTablesAsync()
        {
            try
            {
                await _connection.CreateTableAsync<EmotionRecord>();
                await _connection.CreateTableAsync<MinigameSession>();
                await _connection.CreateTableAsync<UserProfile>();
                await _connection.CreateTableAsync<DiaryEntry>();
                await _connection.CreateTableAsync<InventoryItem>();

                Debug.Log("[DatabaseManager] Tablas verificadas/creadas correctamente.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DatabaseManager] Error al crear tablas: {ex.Message}");
                throw;
            }
        }
    }
}
