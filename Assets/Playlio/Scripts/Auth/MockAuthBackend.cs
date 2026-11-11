using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using CodeStage.AntiCheat.ObscuredTypes;
using UnityEngine;

namespace Playlio.Auth
{
    /// <summary>
    /// Offline fake of the Better Auth email endpoints, for testing without a server.
    /// Accounts live only on this device (encrypted ObscuredPrefs); replies use the same JSON
    /// shapes and error codes as Better Auth, so the rest of the auth code cannot tell the difference.
    /// NOT for release: switch auth_config.json to "mode": "live".
    /// </summary>
    public sealed class MockAuthBackend : IAuthBackend
    {
        private const string AccountsKey = "playlio.mock.accounts";
        private const string CookieName = "mock_session_token=";
        private const int MinPasswordLength = 10;
        private const int MaxPasswordLength = 128;

        [Serializable]
        private class MockAccount
        {
            public string id;
            public string name;
            public string email;
            public string salt;
            public string passwordHash;
        }

        [Serializable]
        private class MockAccountList
        {
            public List<MockAccount> accounts = new List<MockAccount>();
        }

        private readonly ISessionStore sessionStore;
        private readonly float delaySeconds;
        private readonly bool simulateOffline;
        private MockAccountList cache;

        public MockAuthBackend(PlaylioAuthConfig config, ISessionStore store)
        {
            sessionStore = store ?? throw new ArgumentNullException(nameof(store));
            PlaylioAuthConfig.MockSettings mock = config != null ? config.mock : null;
            delaySeconds = mock != null ? Mathf.Max(0f, mock.delaySeconds) : 0.6f;
            simulateOffline = mock != null && mock.simulateOffline;
        }

        public bool HasStoredSession => !string.IsNullOrEmpty(sessionStore.ReadCookie());

        public void ClearSession() => sessionStore.Clear();

        /// <summary>Deletes every mock account and the saved session (editor menu: Playlio > Auth).</summary>
        public static void ResetAll()
        {
            ObscuredPrefs.DeleteKey(AccountsKey);
            new ObscuredPrefsSessionStore().Clear();
            PlayerPrefs.Save();
        }

        public IEnumerator SignUpEmail(string name, string email, string password, Action<ApiResult> done)
        {
            yield return Wait();
            if (simulateOffline) { done?.Invoke(Offline()); yield break; }

            email = Normalize(email);
            if (string.IsNullOrEmpty(email) || email.IndexOf('@') <= 0)
            {
                done?.Invoke(Error(400, "INVALID_EMAIL", "Invalid email"));
                yield break;
            }
            if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            {
                done?.Invoke(Error(400, "PASSWORD_TOO_SHORT", "Password too short"));
                yield break;
            }
            if (password.Length > MaxPasswordLength)
            {
                done?.Invoke(Error(400, "PASSWORD_TOO_LONG", "Password too long"));
                yield break;
            }

            MockAccountList list = Load();
            if (Find(list, email) != null)
            {
                done?.Invoke(Error(422, "USER_ALREADY_EXISTS", "User already exists. Use another email."));
                yield break;
            }

            string salt = Guid.NewGuid().ToString("N");
            var account = new MockAccount
            {
                id = Guid.NewGuid().ToString(),
                name = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim(),
                email = email,
                salt = salt,
                passwordHash = Hash(salt, password),
            };
            list.accounts.Add(account);
            Save(list);

            sessionStore.WriteCookie(CookieName + account.id);   // Better Auth signs in after sign-up
            done?.Invoke(Ok(UserJson(account)));
        }

        public IEnumerator SignInEmail(string email, string password, Action<ApiResult> done)
        {
            yield return Wait();
            if (simulateOffline) { done?.Invoke(Offline()); yield break; }

            MockAccount account = Find(Load(), Normalize(email));
            if (account == null || password == null || account.passwordHash != Hash(account.salt, password))
            {
                done?.Invoke(Error(401, "INVALID_EMAIL_OR_PASSWORD", "Invalid email or password"));
                yield break;
            }

            sessionStore.WriteCookie(CookieName + account.id);
            done?.Invoke(Ok(UserJson(account)));
        }

        public IEnumerator GetSession(Action<ApiResult> done)
        {
            yield return Wait();
            if (simulateOffline) { done?.Invoke(Offline()); yield break; }

            MockAccount account = SessionAccount();
            done?.Invoke(Ok(account != null ? UserJson(account) : "null"));
        }

        public IEnumerator SignOut(Action<ApiResult> done)
        {
            yield return Wait();
            sessionStore.Clear();
            done?.Invoke(simulateOffline ? Offline() : Ok("{\"success\":true}"));
        }

        // ---------------- helpers ----------------

        private object Wait()
        {
            return delaySeconds > 0f ? new WaitForSecondsRealtime(delaySeconds) : null;
        }

        private MockAccount SessionAccount()
        {
            string cookie = sessionStore.ReadCookie();
            if (string.IsNullOrEmpty(cookie) || !cookie.StartsWith(CookieName, StringComparison.Ordinal))
                return null;
            string id = cookie.Substring(CookieName.Length);
            foreach (MockAccount account in Load().accounts)
            {
                if (account.id == id)
                    return account;
            }
            return null;
        }

        private static MockAccount Find(MockAccountList list, string email)
        {
            foreach (MockAccount account in list.accounts)
            {
                if (account.email == email)
                    return account;
            }
            return null;
        }

        private MockAccountList Load()
        {
            if (cache != null)
                return cache;

            cache = new MockAccountList();
            string json = ObscuredPrefs.HasKey(AccountsKey) ? ObscuredPrefs.GetString(AccountsKey, string.Empty) : string.Empty;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    MockAccountList loaded = JsonUtility.FromJson<MockAccountList>(json);
                    if (loaded != null && loaded.accounts != null)
                        cache = loaded;
                }
                catch (ArgumentException)
                {
                    Debug.LogWarning("[Playlio] Mock accounts were unreadable and have been reset.");
                }
            }
            return cache;
        }

        private void Save(MockAccountList list)
        {
            cache = list;
            ObscuredPrefs.SetString(AccountsKey, JsonUtility.ToJson(list));
            PlayerPrefs.Save();
        }

        private static string Normalize(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string Hash(string salt, string password)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(salt + ":" + password));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes)
                    builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        private static string UserJson(MockAccount account)
        {
            var envelope = new AuthEnvelope
            {
                user = new AuthUser { id = account.id, name = account.name, email = account.email },
            };
            return JsonUtility.ToJson(envelope);
        }

        private static ApiResult Ok(string body)
        {
            return new ApiResult { success = true, statusCode = 200, text = body };
        }

        private static ApiResult Error(long status, string code, string message)
        {
            return new ApiResult
            {
                success = false,
                statusCode = status,
                text = JsonUtility.ToJson(new AuthError { code = code, message = message }),
            };
        }

        private static ApiResult Offline()
        {
            return new ApiResult { success = false, statusCode = 0, text = "Cannot connect to destination host (mock offline)" };
        }
    }
}
