using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GeoAds
{
    /// <summary>
    /// Orchestrates location, ad fetching (cached 5 min), and the carousel UI.
    /// NOT named like the existing global AdManager; lives in GeoAds namespace.
    /// No per-frame work: everything is event/coroutine driven.
    ///
    /// Any number of carousels can ask for ads (Request). Requests that arrive while a fetch
    /// is running share that one HTTP call. The newest manager wins, so a fresh copy of the
    /// Game scene (next level / retry) takes over from the previous level's manager.
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

        // static so the cache and the waiting carousels survive a manager hand-over
        private static GeoAdResponse cachedResponse;
        private static double cacheTimestamp;
        private static readonly List<GeoAdCarousel> pending = new List<GeoAdCarousel>();
        private static bool pendingForceRefresh;

        private readonly List<GeoAdCarousel> fillBuffer = new List<GeoAdCarousel>();
        private bool fetchInFlight;
        private GeoAdCarousel runtimeCarousel;

        public static bool IsAvailable => instance != null;

        /// <summary>Fetches ads (or uses the cache) and fills this manager's own carousel.</summary>
        public static void Show(bool forceRefresh = false)
        {
            GeoAdManager mgr = EnsureInstance();
            if (mgr != null)
                mgr.Enqueue(mgr.ResolveDefaultCarousel(), forceRefresh);
        }

        /// <summary>Fetches ads (or uses the cache) and fills the given carousel.</summary>
        public static void Request(GeoAdCarousel target, bool forceRefresh)
        {
            if (target == null)
                return;
            GeoAdManager mgr = EnsureInstance();
            if (mgr != null)
                mgr.Enqueue(target, forceRefresh);
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
            // newest wins: the previous level's manager (still alive because the old Game scene
            // copy is never unloaded) points at a destroyed carousel, so replace it
            if (instance != null && instance != this)
            {
                GeoAdManager previous = instance;
                instance = this;
                Destroy(previous.gameObject);
                return;
            }
            instance = this;
        }

        private void OnEnable()
        {
            // pick up requests left by a replaced manager or made while this one was disabled
            if (instance == this && pending.Count > 0 && !fetchInFlight)
                StartCoroutine(ShowFlow());
        }

        private void OnDisable()
        {
            fetchInFlight = false; // its coroutine stops with the component
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Enqueue(GeoAdCarousel target, bool forceRefresh)
        {
            if (target != null && !pending.Contains(target))
                pending.Add(target);
            if (forceRefresh)
                pendingForceRefresh = true;

            if (!fetchInFlight && isActiveAndEnabled)
                StartCoroutine(ShowFlow());
        }

        private GeoAdCarousel ResolveDefaultCarousel()
        {
            if (carousel != null)
                return carousel;

            if (runtimeCarousel == null)
            {
                var host = new GameObject("GeoAdCarouselRuntime");
                runtimeCarousel = host.AddComponent<GeoAdCarousel>();
            }
            return runtimeCarousel;
        }

        private IEnumerator ShowFlow()
        {
            fetchInFlight = true;

            while (pending.Count > 0)
            {
                GeoAdResponse response = pendingForceRefresh ? null : GetFreshCache();
                if (response == null)
                {
                    GeoAdResponse fetched = null;
                    yield return Fetch(r => fetched = r);
                    response = fetched;
                }

                // everything queued until now (also during the fetch) gets this answer
                pendingForceRefresh = false;
                fillBuffer.Clear();
                fillBuffer.AddRange(pending);
                pending.Clear();

                if (response == null || response.offers == null || response.offers.Length == 0)
                {
                    Debug.LogWarning("[GeoAds] no offers to show.");
                    fillBuffer.Clear();
                    break;
                }

                for (int i = 0; i < fillBuffer.Count; i++)
                {
                    GeoAdCarousel target = fillBuffer[i];
                    if (target != null) // skips carousels destroyed while waiting (closed popup)
                        target.Fill(response.offers);
                }
                fillBuffer.Clear();
            }

            fetchInFlight = false;
        }

        /// <summary>Location + live feed, mock fallback, stale cache as last resort.</summary>
        private IEnumerator Fetch(Action<GeoAdResponse> onDone)
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
                onDone(fresh);
            }
            else
            {
                Debug.LogWarning("[GeoAds] fetch failed: " + (fetchError ?? "unknown"));
                onDone(cachedResponse); // stale cache is better than nothing
            }
        }

        private static GeoAdResponse GetFreshCache()
        {
            if (cachedResponse == null)
                return null;
            return (GetUnixTime() - cacheTimestamp) < CacheLifetimeSeconds ? cachedResponse : null;
        }

        private static double GetUnixTime()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
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
