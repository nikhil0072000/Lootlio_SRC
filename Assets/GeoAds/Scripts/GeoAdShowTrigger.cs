using UnityEngine;

namespace GeoAds
{
    /// <summary>
    /// Scene-side trigger for the static GeoAdManager entry points, so the carousel
    /// can be shown on scene start or from a uGUI Button On Click () slot.
    /// </summary>
    public class GeoAdShowTrigger : MonoBehaviour
    {
        [SerializeField] private bool showOnStart = true;
        [Tooltip("Call the API again every time (each level start) instead of using the 5-minute cache.")]
        [SerializeField] private bool forceRefresh = true;

        private void Start()
        {
            if (showOnStart)
                Show();
        }

        public void Show()
        {
            GeoAdManager.Show(forceRefresh);
        }

        public void Hide()
        {
            GeoAdManager.Hide();
        }
    }
}
