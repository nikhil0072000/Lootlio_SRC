using System;
using UnityEngine;

namespace Playlio.Auth
{
    [Serializable] public class AuthUser { public string id; public string name; public string email; }
    [Serializable] public class AuthEnvelope { public AuthUser user; }
    [Serializable] public class AuthError { public string code; public string message; }

    /// <summary>
    /// Account state for the game: who is signed in, plus sign-up / sign-in / sign-out / restore.
    /// Lives on the persistent "PlaylioAuth" object created by PlaylioAuth (bootstrap).
    /// </summary>
    public sealed class PlaylioAuthService : MonoBehaviour
    {
        public event Action SignedIn;
        public event Action SignedOut;

        private IAuthBackend api;

        public bool IsSignedIn { get; private set; }
        public string UserId { get; private set; }
        public string UserName { get; private set; }
        public string UserEmail { get; private set; }
        public bool IsConfigured => api != null;
        public bool IsMock => api is MockAuthBackend;

        /// <summary>backend: PlaylioApiClient (live server) or MockAuthBackend (offline testing).</summary>
        public void Initialize(IAuthBackend backend)
        {
            api = backend;
        }

        /// <summary>
        /// Checks the saved session with the server. done(true) = signed in.
        /// Offline with a saved session counts as signed in, so the player is not asked again.
        /// </summary>
        public void RestoreSession(Action<bool> done)
        {
            if (api == null || !api.HasStoredSession)
            {
                done?.Invoke(false);
                return;
            }

            StartCoroutine(api.GetSession(result =>
            {
                if (result.success)
                {
                    AuthUser user = ParseUser(result.text);
                    if (user != null)
                    {
                        SetSignedIn(user);
                        done?.Invoke(true);
                        return;
                    }
                    api.ClearSession();   // "null" body: the session expired
                    done?.Invoke(false);
                    return;
                }

                if (result.statusCode == 0)
                {
                    // no network: keep the saved session and let the player in
                    IsSignedIn = true;
                    done?.Invoke(true);
                    return;
                }

                api.ClearSession();
                done?.Invoke(false);
            }));
        }

        public void SignUp(string name, string email, string password, Action<bool, string> done)
        {
            if (api == null)
            {
                done?.Invoke(false, "Sign-in is not configured.");
                return;
            }
            StartCoroutine(api.SignUpEmail(name, email, password, result => Finish(result, true, done)));
        }

        public void SignIn(string email, string password, Action<bool, string> done)
        {
            if (api == null)
            {
                done?.Invoke(false, "Sign-in is not configured.");
                return;
            }
            StartCoroutine(api.SignInEmail(email, password, result => Finish(result, false, done)));
        }

        public void SignOut(Action done)
        {
            if (api == null)
            {
                done?.Invoke();
                return;
            }
            StartCoroutine(api.SignOut(_ =>
            {
                api.ClearSession();   // local sign-out even if the server call failed
                IsSignedIn = false;
                UserId = UserName = UserEmail = null;
                SignedOut?.Invoke();
                done?.Invoke();
            }));
        }

        private void Finish(ApiResult result, bool isSignUp, Action<bool, string> done)
        {
            if (!result.success)
            {
                done?.Invoke(false, FriendlyError(result));
                return;
            }

            AuthUser user = ParseUser(result.text) ?? new AuthUser();
            if (!api.HasStoredSession)
                Debug.LogWarning("[Playlio] Signed in, but the server sent no session cookie; the player will be asked again next launch.");

            SetSignedIn(user);
            done?.Invoke(true, isSignUp ? "Account created. You're signed in." : "Signed in successfully.");
        }

        private void SetSignedIn(AuthUser user)
        {
            IsSignedIn = true;
            UserId = user.id;
            UserName = user.name;
            UserEmail = user.email;
            SignedIn?.Invoke();
        }

        private static AuthUser ParseUser(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            string trimmed = json.Trim();
            if (trimmed == "null" || !trimmed.StartsWith("{", StringComparison.Ordinal))
                return null;
            try
            {
                AuthEnvelope envelope = JsonUtility.FromJson<AuthEnvelope>(trimmed);
                return envelope != null && envelope.user != null && !string.IsNullOrEmpty(envelope.user.id)
                    ? envelope.user
                    : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>Turns Better Auth error replies into short messages for the player.</summary>
        private static string FriendlyError(ApiResult result)
        {
            if (result.statusCode == 0)
                return "Can't reach the server. Check your internet connection.";

            AuthError error = null;
            string body = result.text != null ? result.text.Trim() : string.Empty;
            if (body.StartsWith("{", StringComparison.Ordinal))
            {
                try { error = JsonUtility.FromJson<AuthError>(body); }
                catch (ArgumentException) { error = null; }
            }

            string code = error != null && error.code != null ? error.code.ToUpperInvariant() : string.Empty;
            if (code.Contains("INVALID_EMAIL_OR_PASSWORD") || code.Contains("INVALID_PASSWORD"))
                return "Wrong email or password.";
            if (code.Contains("USER_ALREADY_EXISTS"))
                return "Email already in use.";
            if (code.Contains("PASSWORD_TOO_SHORT"))
                return "Password must contain at least 10 characters.";
            if (code.Contains("PASSWORD_TOO_LONG"))
                return "Password must be 128 characters or fewer.";
            if (code.Contains("INVALID_EMAIL"))
                return "Enter a valid email address.";
            if (code.Contains("ORIGIN") || result.statusCode == 403)
                return "The server refused this app. Check \"origin\" in auth_config.json.";
            if (result.statusCode == 429)
                return "Too many attempts. Please wait a moment.";

            if (error != null && !string.IsNullOrEmpty(error.message))
                return error.message;
            return "Something went wrong (HTTP " + result.statusCode + ").";
        }
    }
}
