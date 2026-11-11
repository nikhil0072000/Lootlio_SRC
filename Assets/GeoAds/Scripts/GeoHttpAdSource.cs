using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace GeoAds
{
    /// <summary>
    /// HTTP ad source.
    /// - Full URL ("https://lootlio-one.vercel.app/api/mock"): GET it verbatim, no query string,
    ///   because the endpoint answers with the whole geo feed for the caller's location.
    /// - Base URL ("https://your-ad-api.example.com"): legacy shape
    ///   GET {baseUrl}/api/ads/nearby?lat=..&lng=..&radius=..
    /// Expects the same JSON shape as the mock (bare array of offers or {"offers":[...]}, or any
    /// envelope that carries a top-level "offers" array).
    /// </summary>
    public class GeoHttpAdSource : IGeoAdSource
    {
        private readonly string baseUrl;
        private readonly string fullUrl;

        public GeoHttpAdSource(string endpoint)
        {
            endpoint = (endpoint ?? string.Empty).Trim();
            if (endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                fullUrl = endpoint;
                baseUrl = null;
            }
            else
            {
                baseUrl = endpoint.TrimEnd('/');
                fullUrl = null;
            }
        }

        public IEnumerator FetchOffers(double lat, double lng, double radiusKm, Action<GeoAdResponse> onSuccess, Action<string> onError)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string url;
            if (fullUrl != null)
            {
                // Send the device location to the server (only when a location was found).
                bool hasLocation = lat != 0d || lng != 0d;
                url = hasLocation
                    ? string.Format(inv, "{0}{1}lat={2}&lng={3}&radius={4}",
                        fullUrl,
                        fullUrl.Contains("?") ? "&" : "?",
                        lat.ToString("R", inv),
                        lng.ToString("R", inv),
                        radiusKm.ToString("R", inv))
                    : fullUrl;
            }
            else
            {
                url = string.Format(
                    "{0}/api/ads/nearby?lat={1}&lng={2}&radius={3}",
                    baseUrl,
                    lat.ToString("R", inv),
                    lng.ToString("R", inv),
                    radiusKm.ToString("R", inv));
            }

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                bool failed = request.result != UnityWebRequest.Result.Success;
#else
                bool failed = request.isNetworkError || request.isHttpError;
#endif
                if (failed)
                {
                    onError?.Invoke("GeoAds HTTP " + request.responseCode + ": " + request.error);
                    yield break;
                }

                GeoAdResponse response;
                try
                {
                    string json = "{\"offers\":" + request.downloadHandler.text + "}";
                    string trimmed = request.downloadHandler.text.TrimStart();
                    if (trimmed.StartsWith("{"))
                        json = request.downloadHandler.text;

                    response = JsonUtility.FromJson<GeoAdResponse>(json);
                    if (response == null || response.offers == null || response.offers.Length == 0)
                    {
                        onError?.Invoke("GeoAds HTTP: response contains no offers.");
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    onError?.Invoke("GeoAds HTTP: JSON parse failed: " + ex.Message);
                    yield break;
                }

                response.source = "http";
                response.fetchedAt = DateTime.UtcNow.ToString("o");
                onSuccess?.Invoke(response);
            }
        }
    }
}