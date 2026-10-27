using System;

namespace GeoAds
{
    /// <summary>
    /// A single geo/coupon offer returned by an ad source.
    /// JsonUtility-compatible (no Newtonsoft dependency). Unknown fields in the
    /// response (userLocation, carousel, popup, meta, ...) are ignored by JsonUtility.
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
        public string placeholderColor; // "#RRGGBB" or "RRGGBBAA" used when no image is available
        public double latitude;
        public double longitude;
        public double distanceKm;
        public string storeName;
        public bool isCoupon;
        public GeoCouponData coupon;

        // --- fields carried by https://lootlio-one.vercel.app/api/mock ---
        public string type;        // "affiliate" | "merchant_offer" (drives the neon frame colour)
        public string category;    // "Food & Dining", "Electronics", ...
        public string ctaText;     // "Copy Code & Order" - the button caption
        public string actionUrl;   // opened in the system browser when the card is tapped
    }

    /// <summary>
    /// Coupon details attached to an offer.
    /// </summary>
    [Serializable]
    public class GeoCouponData
    {
        public string code;
        public string discountText;
        public string startsAt;    // ISO 8601 string; parsed defensively
        public string expiresAt;   // ISO 8601 string; parsed defensively
        public string terms;
    }
}