using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Lutra.Core.Architecture;

namespace Lutra.Core.Systems
{
    /// <summary>
    /// Gestiona la autenticación de usuarios a través de Firebase Auth.
    /// Expone métodos de registro, login y logout, y mantiene la referencia
    /// al usuario autenticado durante la sesión.
    ///
    /// No tiene Awake() propio: el registro en ServiceLocator lo realiza GameManager
    /// llamando a RegisterSelf() en _registerServices(). La desregistración la hereda
    /// de BaseService.OnDestroy().
    /// </summary>
    public class AuthManager : BaseService
    {
        // ── Firebase ───────────────────────────────────────────────────

        private FirebaseAuth _auth;
        private FirebaseUser _currentUser;

        // ── Propiedades públicas ───────────────────────────────────────

        /// <summary>Devuelve true si hay un usuario autenticado en esta sesión.</summary>
        public bool IsLoggedIn => _currentUser != null;

        /// <summary>UID del usuario activo, o null si no hay sesión.</summary>
        public string CurrentUserId => _currentUser?.UserId;

        /// <summary>Email del usuario activo, o null si no hay sesión.</summary>
        public string CurrentEmail => _currentUser?.Email;

        // ── Inicialización ─────────────────────────────────────────────

        /// <summary>
        /// Comprueba y repara las dependencias de Firebase, inicializa FirebaseAuth
        /// y recupera la sesión activa si existiera.
        /// Debe llamarse desde GameManager antes de cualquier otro uso.
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                var status = await FirebaseApp.CheckAndFixDependenciesAsync();

                if (status != DependencyStatus.Available)
                {
                    Debug.LogError($"[AuthManager] Firebase no disponible: {status}");
                    return false;
                }

                _auth        = FirebaseAuth.DefaultInstance;
                _currentUser = _auth.CurrentUser;

                Debug.Log(_currentUser != null
                    ? $"[AuthManager] Sesión restaurada: {_currentUser.Email}"
                    : "[AuthManager] Firebase listo. Sin sesión activa.");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthManager] InitializeAsync: {ex.Message}");
                return false;
            }
        }

        // ── Registro ───────────────────────────────────────────────────

        /// <summary>
        /// Crea una cuenta con email y contraseña.
        /// La contraseña debe tener mínimo 8 caracteres, una mayúscula y un número.
        /// </summary>
        /// <returns>
        /// Tupla (true, null) si el registro fue correcto,
        /// o (false, mensajeDeError) si falló.
        /// </returns>
        public async Task<(bool success, string error)> RegisterWithEmail(string email, string password)
        {
            // Validaciones locales antes de llamar a Firebase
            if (string.IsNullOrWhiteSpace(email))
                return (false, "El email no puede estar vacío");

            string passwordError = _validatePassword(password);
            if (passwordError != null)
                return (false, passwordError);

            try
            {
                var result   = await _auth.CreateUserWithEmailAndPasswordAsync(email, password);
                _currentUser = result.User;

                Debug.Log($"[AuthManager] Cuenta creada: {_currentUser.Email}");
                return (true, null);
            }
            catch (AggregateException agg)
            {
                return (false, _getFirebaseErrorMessage(agg.GetBaseException()));
            }
            catch (Exception ex)
            {
                return (false, _getFirebaseErrorMessage(ex));
            }
        }

        // ── Login ──────────────────────────────────────────────────────

        /// <summary>
        /// Inicia sesión con email y contraseña.
        /// </summary>
        /// <returns>
        /// Tupla (true, null) si el login fue correcto,
        /// o (false, mensajeDeError) si falló.
        /// </returns>
        public async Task<(bool success, string error)> LoginWithEmail(string email, string password)
        {
            try
            {
                // Limpiar sesión previa si existe para evitar tokens corruptos
                if (_auth?.CurrentUser != null)
                {
                    _auth.SignOut();
                    _currentUser = null;
                }

                var result   = await _auth.SignInWithEmailAndPasswordAsync(email, password);
                _currentUser = result.User;

                Debug.Log($"[AuthManager] Sesión iniciada: {_currentUser.Email}");
                return (true, null);
            }
            catch (AggregateException agg)
            {
                return (false, _getFirebaseErrorMessage(agg.GetBaseException()));
            }
            catch (Exception ex)
            {
                return (false, _getFirebaseErrorMessage(ex));
            }
        }

        // ── Recuperación de contraseña ─────────────────────────────────

        /// <summary>
        /// Envía un email de recuperación de contraseña a la dirección indicada.
        /// </summary>
        /// <returns>
        /// Tupla (true, null) si el envío fue correcto,
        /// o (false, mensajeDeError) si falló.
        /// </returns>
        public async Task<(bool success, string error)> SendPasswordResetEmail(string email)
        {
            try
            {
                await _auth.SendPasswordResetEmailAsync(email);
                Debug.Log($"[AuthManager] Email de recuperación enviado a {email}");
                return (true, null);
            }
            catch (AggregateException agg)
            {
                return (false, _getFirebaseErrorMessage(agg.GetBaseException()));
            }
            catch (Exception ex)
            {
                return (false, _getFirebaseErrorMessage(ex));
            }
        }

        // ── Logout ─────────────────────────────────────────────────────

        /// <summary>
        /// Recarga el usuario actual desde Firebase para verificar que la cuenta sigue siendo válida.
        /// Lanza excepción si no hay usuario activo o si Firebase rechaza el token.
        /// </summary>
        public async Task RefreshCurrentUser()
        {
            if (_currentUser == null) throw new Exception("No hay usuario activo");
            await _currentUser.ReloadAsync();
        }

        /// <summary>Cierra la sesión actual en Firebase.</summary>
        public void Logout()
        {
            _auth?.SignOut();
            _currentUser = null;
            Debug.Log("[AuthManager] Sesión cerrada.");
        }

        // ── Cambio de contraseña ───────────────────────────────────────

        /// <summary>
        /// Re-autentica al usuario con su contraseña actual y la cambia por la nueva.
        /// Lanza <see cref="Exception"/> con mensaje en español si la operación falla.
        /// </summary>
        public async Task ChangePassword(string oldPassword, string newPassword)
        {
            if (_currentUser == null)
                throw new Exception("No hay usuario activo");

            string passwordError = _validatePassword(newPassword);
            if (passwordError != null)
                throw new Exception(passwordError);

            try
            {
                var credential = EmailAuthProvider.GetCredential(_currentUser.Email, oldPassword);
                await _currentUser.ReauthenticateAndRetrieveDataAsync(credential);
                await _currentUser.UpdatePasswordAsync(newPassword);
                Debug.Log("[AuthManager] Contraseña actualizada correctamente.");
            }
            catch (AggregateException agg)
            {
                throw new Exception(_getFirebaseErrorMessage(agg.GetBaseException()));
            }
            catch (Exception ex)
            {
                throw new Exception(_getFirebaseErrorMessage(ex));
            }
        }

        // ── Eliminación de cuenta ──────────────────────────────────────

        /// <summary>
        /// Elimina la cuenta del usuario autenticado en Firebase Auth.
        /// Lanza <see cref="Exception"/> con mensaje en español si la operación falla.
        /// </summary>
        public async Task DeleteAccount()
        {
            if (_currentUser == null)
                throw new Exception("No hay usuario activo");

            try
            {
                await _currentUser.DeleteAsync();
                _currentUser = null;
                _auth?.SignOut();
                Debug.Log("[AuthManager] Cuenta eliminada correctamente.");
            }
            catch (AggregateException agg)
            {
                throw new Exception(_getFirebaseErrorMessage(agg.GetBaseException()));
            }
            catch (Exception ex)
            {
                throw new Exception(_getFirebaseErrorMessage(ex));
            }
        }

        // ── Helpers privados ───────────────────────────────────────────

        /// <summary>
        /// Valida los requisitos mínimos de la contraseña.
        /// </summary>
        /// <returns>null si la contraseña es válida; mensaje de error en español si no lo es.</returns>
        private string _validatePassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return "La contraseña debe tener al menos 8 caracteres";

            if (!Regex.IsMatch(password, "[A-Z]"))
                return "La contraseña debe incluir al menos una letra mayúscula";

            if (!Regex.IsMatch(password, "[0-9]"))
                return "La contraseña debe incluir al menos un número";

            return null;
        }

        /// <summary>
        /// Traduce una excepción de Firebase a un mensaje de error en español.
        /// </summary>
        private string _getFirebaseErrorMessage(Exception ex)
        {
            Debug.LogError($"[AuthManager] Firebase error: {ex.GetType().Name}: {ex.Message}");

            if (ex is FirebaseException fbEx)
            {
                var code = (AuthError)fbEx.ErrorCode;
                Debug.LogError($"[AuthManager] AuthError code: {code} ({fbEx.ErrorCode})");

                return code switch
                {
                    AuthError.EmailAlreadyInUse    => "Este email ya está registrado",
                    AuthError.WrongPassword        => "Contraseña incorrecta",
                    AuthError.UserNotFound         => "No existe cuenta con este email",
                    AuthError.InvalidEmail         => "El formato del email no es válido",
                    AuthError.WeakPassword         => "La contraseña es demasiado débil",
                    AuthError.NetworkRequestFailed => "Sin conexión. Comprueba tu internet",
                    AuthError.TooManyRequests      => "Demasiados intentos. Espera un momento",
                    AuthError.UserDisabled         => "Esta cuenta ha sido desactivada",
                    AuthError.InvalidCredential    => "Credenciales incorrectas",
                    AuthError.SessionExpired       => "Sesión expirada. Inicia sesión de nuevo",
                    AuthError.Failure              => "Sesión expirada. Inicia sesión de nuevo",
                    _                              => $"Error ({code}). Inténtalo de nuevo"
                };
            }

            return $"Error de conexión: {ex.Message}";
        }
    }
}
