using System;

namespace GeoAds
{
    /// <summary>
    /// Top-level response envelope for an ad fetch.
    /// </summary>
    [Serializable]
    public class GeoAdResponse
    {
        public GeoAdOffer[] offers;
        public string source;        // "mock" or "http"
        public string fetchedAt;     // ISO 8601
    }
}
