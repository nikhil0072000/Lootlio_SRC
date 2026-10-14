using System;
using System.Collections;

namespace GeoAds
{
    /// <summary>
    /// A source of geo-targeted ad offers. Implementations must yield one result
    /// (success list or error) exactly once.
    /// </summary>
    public interface IGeoAdSource
    {
        IEnumerator FetchOffers(double lat, double lng, double radiusKm, Action<GeoAdResponse> onSuccess, Action<string> onError);
    }
}
