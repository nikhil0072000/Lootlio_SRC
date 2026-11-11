using UnityEngine;

namespace Playlio.Auth
{
    /// <summary>
    /// Starts account sign-in automatically when the game launches (no scene setup needed):
    /// creates one persistent "PlaylioAuth" object, picks the backend from auth_config.json
    /// ("mode": "mock" = offline fake for testing, "live" = real Better Auth server),
    /// restores a saved session silently, and shows the Sign In / Sign Up screen when nobody is signed in.
    /// Other features use PlaylioAuth.Service (IsSignedIn, UserName, SignOut, events).
    /// </summary>
    public static class PlaylioAuth
    {
        public static PlaylioAuthService Service { get; private set; }
        public static PlaylioAuthConfig Config { get; private set; }

        private static Transform root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Service != null)
                return;

            Config = PlaylioAuthConfig.Load();

            var go = new GameObject("PlaylioAuth");
            Object.DontDestroyOnLoad(go);
            root = go.transform;

            Service = go.AddComponent<PlaylioAuthService>();

            IAuthBackend backend = CreateBackend(Config);
            if (backend == null)
                return;

            Service.Initialize(backend);
            Service.RestoreSession(signedIn =>
            {
                if (!signedIn && Config.showOnLaunch)
                    ShowSignInScreen();
            });
        }

        private static IAuthBackend CreateBackend(PlaylioAuthConfig config)
        {
            var store = new ObscuredPrefsSessionStore();

            if (config.IsMock)
            {
                Debug.Log("[Playlio] Auth is in MOCK mode (no server). "
                    + "Set \"mode\": \"live\" in Assets/Resources/Playlio/auth_config.json before release.");
                return new MockAuthBackend(config, store);
            }

            if (!config.HasServer)
            {
                Debug.LogWarning("[Playlio] auth_config.json apiBaseUrl is empty - sign-in is turned off. "
                    + "Set it in Assets/Resources/Playlio/auth_config.json, or use \"mode\": \"mock\" to test.");
                return null;
            }

            return new PlaylioApiClient(config, store);
        }

        /// <summary>Opens the Sign In / Sign Up screen (e.g. from a Settings button). Null if sign-in is off.</summary>
        public static PlaylioAuthScreen ShowSignInScreen()
        {
            if (Service == null || root == null || !Service.IsConfigured)
                return null;
            return PlaylioAuthScreen.Show(root, Service);
        }

        /// <summary>Signs out and, when the screen is enabled, shows it again.</summary>
        public static void SignOut()
        {
            if (Service == null || !Service.IsConfigured)
                return;
            Service.SignOut(() =>
            {
                if (Config != null && Config.showOnLaunch)
                    ShowSignInScreen();
            });
        }
    }
}
