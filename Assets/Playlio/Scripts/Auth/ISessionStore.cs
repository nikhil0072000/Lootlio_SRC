using CodeStage.AntiCheat.ObscuredTypes;
using UnityEngine;

namespace Playlio.Auth
{
    /// <summary>Keeps the Better Auth session cookie ("name=value" pair only).</summary>
    public interface ISessionStore
    {
        string ReadCookie();
        void WriteCookie(string cookie);
        void Clear();
    }

    /// <summary>Development store: forgets the session when the app closes.</summary>
    public sealed class MemorySessionStore : ISessionStore
    {
        private string cookie;
        public string ReadCookie() => cookie;
        public void WriteCookie(string value) => cookie = value;
        public void Clear() => cookie = null;
    }

    /// <summary>
    /// Keeps the session across app restarts, encrypted with Anti-Cheat Toolkit's ObscuredPrefs.
    /// Better than plain PlayerPrefs, but not OS-secure storage: replace with an
    /// Android Keystore / iOS Keychain store before release.
    /// </summary>
    public sealed class ObscuredPrefsSessionStore : ISessionStore
    {
        private const string Key = "playlio.session";

        private string cached;
        private bool loaded;

        public string ReadCookie()
        {
            if (!loaded)
            {
                cached = ObscuredPrefs.HasKey(Key) ? ObscuredPrefs.GetString(Key, string.Empty) : string.Empty;
                loaded = true;
            }
            return string.IsNullOrEmpty(cached) ? null : cached;
        }

        public void WriteCookie(string value)
        {
            cached = value ?? string.Empty;
            loaded = true;
            ObscuredPrefs.SetString(Key, cached);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            cached = string.Empty;
            loaded = true;
            ObscuredPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
