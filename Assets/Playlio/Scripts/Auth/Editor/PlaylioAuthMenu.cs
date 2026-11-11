using UnityEditor;
using UnityEngine;

namespace Playlio.Auth.Editor
{
    /// <summary>Editor-only helpers for testing sign-in: top menu Playlio > Auth.</summary>
    public static class PlaylioAuthMenu
    {
        private const string ResetItem = "Playlio/Auth/Reset Mock Accounts and Session";
        private const string SignOutItem = "Playlio/Auth/Sign Out (Play Mode)";

        [MenuItem(ResetItem)]
        private static void ResetMock()
        {
            if (!EditorUtility.DisplayDialog("Reset mock sign-in",
                    "Delete every mock account and the saved session on this computer?", "Reset", "Cancel"))
                return;

            MockAuthBackend.ResetAll();
            Debug.Log("[Playlio] Mock accounts and saved session deleted.");
        }

        [MenuItem(SignOutItem)]
        private static void SignOut()
        {
            PlaylioAuth.SignOut();
            Debug.Log("[Playlio] Signed out.");
        }

        [MenuItem(SignOutItem, true)]
        private static bool CanSignOut()
        {
            return Application.isPlaying && PlaylioAuth.Service != null && PlaylioAuth.Service.IsSignedIn;
        }
    }
}
