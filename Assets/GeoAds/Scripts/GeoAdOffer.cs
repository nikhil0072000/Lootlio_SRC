using System;

namespace GeoAds
{
    /// <summary>
    /// A single geo/coupon offer returned by an ad source.
    /// JsonUtility-compatible (no Newtonsoft dependency).
    /// </summary>
    [Serializable]
    public class GeoAdOffer
    {
        public string id;
        public string title;
        public string description;
        public string imageUrl;      // legacy single image; used as product fallback
        public string businessLogoUrl; // small square logo (Assets/GeoAds/logo/* or https URL)
        public string productImageUrl; // large product photo (Assets/GeoAds/Product/* or https URL)
        public string placeholderColor; // "RRGGBBAA" hex used when no image is available
        public double latitude;
        public double longitude;
        public double distanceKm;
        public string storeName;
        public bool isCoupon;
        public GeoCouponData coupon;
    }

    /// <summary>
    /// Coupon details attached to an offer.
    /// </summary>
    [Serializable]
    public class GeoCouponData
    {
        public string code;
        public string discountText;
        public string expiresAt;   // ISO 8601 string; parsed defensively
        public string terms;
    }
}
