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

        private void Start()
        {
            if (showOnStart)
                Show();
        }

        public void Show()
        {
            GeoAdManager.Show();
        }

        public void Hide()
        {
            GeoAdManager.Hide();
        }
    }
}
