using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace GeoAds
{
    /// <summary>
    /// Resolves the user's location:
    /// - Editor OR mock mode: returns Bengaluru (12.9716, 77.5946) deterministically.
    /// - Device + live mode: Input.location permission flow, then GPS; falls back to
    ///   featured ads (no location) after a timeout so the feature always degrades safely.
    /// </summary>
    public static class GeoLocationProvider
    {
        public const double MockLatitude = 12.9716;
        public const double MockLongitude = 77.5946;
        private const float MaxWaitSeconds = 12f;
        private const float DesiredAccuracyMeters = 50f;
#if UNITY_ANDROID
        private const float PermissionWaitSeconds = 60f;
        private static bool androidPermissionAsked;
#endif

        /// <summary>
        /// Yields exactly one LocationResult. never throws.
        /// </summary>
        public struct LocationResult
        {
            public bool hasLocation;
            public double latitude;
            public double longitude;
            public string error;
        }

        public static IEnumerator GetLocation(bool useMock, Action<LocationResult> onDone)
        {
            // 1) Editor or mock: deterministic city center.
            if (useMock || Application.isEditor)
            {
                onDone?.Invoke(new LocationResult
                {
                    hasLocation = true,
                    latitude = MockLatitude,
                    longitude = MockLongitude,
                });
                yield break;
            }

            // 2) Device: permission flow via Input.location.
#if UNITY_ANDROID
            // Android never prompts by itself: ask once, then wait for the player's answer.
            if (!androidPermissionAsked && !Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                androidPermissionAsked = true;
                bool answered = false;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => answered = true;
                callbacks.PermissionDenied += _ => answered = true;
                Permission.RequestUserPermission(Permission.FineLocation, callbacks);

                float waited = 0f;
                while (!answered && waited < PermissionWaitSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
#endif
            if (!Input.location.isEnabledByUser)
            {
                onDone?.Invoke(new LocationResult { hasLocation = false, error = "location-disabled" });
                yield break;
            }

            Input.location.Start(DesiredAccuracyMeters, DesiredAccuracyMeters);
            if (Input.location.status == LocationServiceStatus.Failed)
            {
                Input.location.Stop();
                onDone?.Invoke(new LocationResult { hasLocation = false, error = "start-failed" });
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < MaxWaitSeconds && Input.location.status == LocationServiceStatus.Initializing)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Input.location.Stop();
                onDone?.Invoke(new LocationResult { hasLocation = false, error = "timeout" });
                yield break;
            }

            LocationInfo info = Input.location.lastData;
            Input.location.Stop();

            onDone?.Invoke(new LocationResult
            {
                hasLocation = info.latitude != 0d || info.longitude != 0d,
                latitude = info.latitude,
                longitude = info.longitude,
            });
        }
    }
}
