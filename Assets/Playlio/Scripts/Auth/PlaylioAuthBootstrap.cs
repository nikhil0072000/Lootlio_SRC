using UnityEngine;

namespace Playlio.Auth
{
    /// <summary>
    /// Starts account sign-in automatically when the game launches (no scene setup needed):
    /// creates one persistent "PlaylioAuth" object, restores a saved session silently, and
    /// shows the Sign In / Sign Up screen when nobody is signed in.
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

            if (!Config.HasServer)
            {
                Debug.LogWarning("[Playlio] auth_config.json apiBaseUrl is empty - sign-in is turned off. "
                    + "Set it in Assets/Resources/Playlio/auth_config.json.");
                return;
            }

            Service.Initialize(Config, new ObscuredPrefsSessionStore());
            Service.RestoreSession(signedIn =>
            {
                if (!signedIn && Config.showOnLaunch)
                    ShowSignInScreen();
            });
        }

        /// <summary>Opens the Sign In / Sign Up screen (e.g. from a Settings button). Null if sign-in is off.</summary>
        public static PlaylioAuthScreen ShowSignInScreen()
        {
            if (Service == null || root == null || !Service.IsConfigured)
                return null;
            return PlaylioAuthScreen.Show(root, Service);
        }
    }
}
