using System;
using UnityEngine;

namespace Playlio.Auth
{
    /// <summary>
    /// Server settings read from Assets/Resources/Playlio/auth_config.json.
    /// apiBaseUrl: where the Better Auth server runs (e.g. https://your-app.vercel.app).
    /// origin: must match PLAYLIO_GAME_ORIGIN on the server (Better Auth trustedOrigins).
    /// </summary>
    [Serializable]
    public sealed class PlaylioAuthConfig
    {
        public const string ResourcePath = "Playlio/auth_config";

        public string apiBaseUrl = string.Empty;
        public string origin = string.Empty;
        public bool showOnLaunch = true;

        public bool HasServer => !string.IsNullOrWhiteSpace(apiBaseUrl);

        public static PlaylioAuthConfig Load()
        {
            var config = new PlaylioAuthConfig();
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                Debug.LogWarning("[Playlio] Resources/Playlio/auth_config.json not found.");
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

            config.apiBaseUrl = (config.apiBaseUrl ?? string.Empty).Trim().TrimEnd('/');
            config.origin = (config.origin ?? string.Empty).Trim().TrimEnd('/');
            return config;
        }
    }
}
