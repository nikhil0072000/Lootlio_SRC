using System;
using System.Collections;
using UnityEngine;

namespace GeoAds
{
    /// <summary>
    /// Orchestrates location, ad fetching (cached 5 min), and the carousel UI.
    /// NOT named like the existing global AdManager; lives in GeoAds namespace.
    /// No per-frame work: everything is event/coroutine driven.
    /// </summary>
    public class GeoAdManager : MonoBehaviour
    {
        [SerializeField] private bool useMock = false;
        [Tooltip("Full URL of the geo ad feed. A base URL (no http/https) still uses the legacy /api/ads/nearby path.")]
        [SerializeField] private string apiUrl = "https://lootlio-one.vercel.app/api/mock";
        [SerializeField] private float fetchRadiusKm = 25f;
        [SerializeField] private GeoAdCarousel carousel; // optional; auto-built if null

        private const float CacheLifetimeSeconds = 300f; // 5 minutes

        private static GeoAdManager instance;
        private GeoAdResponse cachedResponse;
        private double cacheTimestamp;
        private bool fetchInFlight;
        private GeoAdCarousel runtimeCarousel;

        public static bool IsAvailable => instance != null;

        /// <summary>Public entry point: nothing else in the project calls this yet.</summary>
        public static void Show()
        {
            GeoAdManager mgr = EnsureInstance();
            if (mgr != null)
                mgr.StartCoroutine(mgr.ShowFlow());
        }

        /// <summary>Public entry point: hides the carousel if visible.</summary>
        public static void Hide()
        {
            if (instance != null)
                instance.HideInternal();
        }

        private static GeoAdManager EnsureInstance()
        {
            if (instance != null)
                return instance;

            var host = new GameObject("GeoAdManager");
            instance = host.AddComponent<GeoAdManager>();
            DontDestroyOnLoad(host);
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private IEnumerator ShowFlow()
        {
            if (fetchInFlight)
                yield break;
            fetchInFlight = true;

            // fetch (or use fresh cache)
            GeoAdResponse response = GetFreshCache();
            if (response == null)
            {
                // location (mocked in editor/mock); no location -> 0,0 is sent
                double lat = 0d;
                double lng = 0d;
                yield return GeoLocationProvider.GetLocation(useMock, result =>
                {
                    if (result.hasLocation)
                    {
                        lat = result.latitude;
                        lng = result.longitude;
                    }
                });

                IGeoAdSource primary = useMock
                    ? (IGeoAdSource)new GeoMockAdSource()
                    : new GeoHttpAdSource(apiUrl);

                bool ok = false;
                GeoAdResponse fresh = null;
                string fetchError = null;

                yield return primary.FetchOffers(lat, lng, fetchRadiusKm,
                    r => { fresh = r; ok = true; },
                    e => { fetchError = e; });

                // live feed failed or came back empty -> bundled mock_ads.json beats an empty strip
                if ((!ok || fresh == null || fresh.offers == null || fresh.offers.Length == 0) && !useMock)
                {
                    Debug.LogWarning("[GeoAds] live feed unusable (" + (fetchError ?? "no offers")
                        + ") - falling back to Resources/GeoAds/mock_ads.json");

                    fetchError = null;
                    ok = false;
                    fresh = null;
                    yield return new GeoMockAdSource().FetchOffers(lat, lng, fetchRadiusKm,
                        r => { fresh = r; ok = true; },
                        e => { fetchError = e; });
                }

                if (ok && fresh != null && fresh.offers != null && fresh.offers.Length > 0)
                {
                    cachedResponse = fresh;
                    cacheTimestamp = GetUnixTime();
                    response = fresh;
                }
                else
                {
                    Debug.LogWarning("[GeoAds] fetch failed: " + (fetchError ?? "unknown"));
                    // stale cache is better than nothing
                    response = cachedResponse;
                }
            }

            // release the guard here so a cache hit does not lock out every later Show()
            fetchInFlight = false;

            if (response == null || response.offers == null || response.offers.Length == 0)
            {
                Debug.LogWarning("[GeoAds] no offers to show.");
                yield break;
            }

            ShowCarousel(response);
        }

        private GeoAdResponse GetFreshCache()
        {
            if (cachedResponse == null)
                return null;
            return (GetUnixTime() - cacheTimestamp) < CacheLifetimeSeconds ? cachedResponse : null;
        }

        private static double GetUnixTime()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        }

        private void ShowCarousel(GeoAdResponse response)
        {
            if (carousel != null)
            {
                carousel.Populate(response.offers);
                carousel.Show();
                return;
            }

            if (runtimeCarousel == null)
            {
                var host = new GameObject("GeoAdCarouselRuntime");
                runtimeCarousel = host.AddComponent<GeoAdCarousel>();
            }
            runtimeCarousel.Populate(response.offers);
            runtimeCarousel.Show();
        }

        private void HideInternal()
        {
            if (carousel != null)
                carousel.Hide();
            if (runtimeCarousel != null)
                runtimeCarousel.Hide();
        }
    }
}