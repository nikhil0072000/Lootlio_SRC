using System;
using UnityEngine;

namespace Playlio.Auth
{
    /// <summary>
    /// Auth settings read from Assets/Resources/Playlio/auth_config.json.
    /// mode: "mock" = offline fake server for testing, "live" = the real Better Auth server.
    /// apiBaseUrl / origin: live server address; origin must match PLAYLIO_GAME_ORIGIN on the server.
    /// endpoints: the four server routes, change them if your real API uses different paths.
    /// mock: fake network delay and an offline switch for testing.
    /// </summary>
    [Serializable]
    public sealed class PlaylioAuthConfig
    {
        public const string ResourcePath = "Playlio/auth_config";
        public const string ModeMock = "mock";
        public const string ModeLive = "live";

        [Serializable]
        public sealed class Endpoints
        {
            public const string DefaultSignUp = "/api/auth/sign-up/email";
            public const string DefaultSignIn = "/api/auth/sign-in/email";
            public const string DefaultSession = "/api/auth/get-session";
            public const string DefaultSignOut = "/api/auth/sign-out";

            public string signUp = DefaultSignUp;
            public string signIn = DefaultSignIn;
            public string session = DefaultSession;
            public string signOut = DefaultSignOut;

            internal void ApplyDefaults()
            {
                signUp = CleanPath(signUp, DefaultSignUp);
                signIn = CleanPath(signIn, DefaultSignIn);
                session = CleanPath(session, DefaultSession);
                signOut = CleanPath(signOut, DefaultSignOut);
            }

            private static string CleanPath(string value, string fallback)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return fallback;
                value = value.Trim();
                return value.StartsWith("/", StringComparison.Ordinal) ? value : "/" + value;
            }
        }

        [Serializable]
        public sealed class MockSettings
        {
            public float delaySeconds = 0.6f;
            public bool simulateOffline;
        }

        public string mode = ModeMock;
        public string apiBaseUrl = string.Empty;
        public string origin = string.Empty;
        public bool showOnLaunch = true;
        public Endpoints endpoints = new Endpoints();
        public MockSettings mock = new MockSettings();

        public bool IsMock => !string.Equals(mode, ModeLive, StringComparison.OrdinalIgnoreCase);
        public bool HasServer => !string.IsNullOrWhiteSpace(apiBaseUrl);

        public static PlaylioAuthConfig Load()
        {
            var config = new PlaylioAuthConfig();
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                Debug.LogWarning("[Playlio] Resources/Playlio/auth_config.json not found - using mock mode defaults.");
            }
            else
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(asset.text, config);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Playlio] auth_config.json could not be read: " + ex.Message);
                }
            }

            config.mode = (config.mode ?? ModeMock).Trim().ToLowerInvariant();
            config.apiBaseUrl = (config.apiBaseUrl ?? string.Empty).Trim().TrimEnd('/');
            config.origin = (config.origin ?? string.Empty).Trim().TrimEnd('/');
            if (config.endpoints == null)
                config.endpoints = new Endpoints();
            config.endpoints.ApplyDefaults();
            if (config.mock == null)
                config.mock = new MockSettings();
            return config;
        }
    }
}
