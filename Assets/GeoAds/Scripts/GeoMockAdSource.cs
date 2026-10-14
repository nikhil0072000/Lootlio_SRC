using System;
using System.Collections;
using UnityEngine;

namespace GeoAds
{
    /// <summary>
    /// Mock ad source: loads Assets/GeoAds/Resources/GeoAds/mock_ads.json via Resources,
    /// waits 0.5 s, then returns the parsed offers. Offline-safe, editor-friendly.
    /// </summary>
    public class GeoMockAdSource : IGeoAdSource
    {
        public IEnumerator FetchOffers(double lat, double lng, double radiusKm, Action<GeoAdResponse> onSuccess, Action<string> onError)
        {
            yield return new WaitForSecondsRealtime(0.5f);

            TextAsset asset = Resources.Load<TextAsset>("GeoAds/mock_ads");
            if (asset == null)
            {
                onError?.Invoke("GeoAds mock: Resources/GeoAds/mock_ads.json not found.");
                yield break;
            }

            GeoAdResponse response;
            try
            {
                response = JsonUtility.FromJson<GeoAdResponse>(JsonHelper.WrapArray(asset.text));
                if (response == null || response.offers == null || response.offers.Length == 0)
                {
                    onError?.Invoke("GeoAds mock: JSON parsed but contains no offers.");
                    yield break;
                }
            }
            catch (Exception ex)
            {
                onError?.Invoke("GeoAds mock: JSON parse failed: " + ex.Message);
                yield break;
            }

            response.source = "mock";
            response.fetchedAt = DateTime.UtcNow.ToString("o");
            onSuccess?.Invoke(response);
        }

        /// <summary>JsonUtility cannot parse a bare top-level array; wrap once.</summary>
        internal static class JsonHelper
        {
            [Serializable]
            private class Wrapper { public GeoAdOffer[] offers; }

            public static string WrapArray(string raw)
            {
                string trimmed = raw.TrimStart();
                if (trimmed.StartsWith("{"))
                    return raw;
                return "{\"offers\":" + raw + "}";
            }
        }
    }
}
