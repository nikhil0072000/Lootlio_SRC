using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace GeoAds
{
    /// <summary>
    /// One coupon/ad card. Two ways to use it:
    /// - prefab (the normal path): the card and its children already exist in
    ///   Assets/GeoAds/Prefabs/GeoAdCarousel.prefab and are assigned in the Inspector; Bind()
    ///   only fills them, nothing is created - so every rect stays editable in the editor.
    /// - code-built (GeoAdCarousel with no prefab): Build() creates the visuals at runtime.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class GeoAdCard : MonoBehaviour
    {
        [Header("Prefab labels")]
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI descLabel;
        [SerializeField] private TextMeshProUGUI discountLabel;
        [SerializeField] private TextMeshProUGUI categoryLabel;
        [SerializeField] private TextMeshProUGUI distanceLabel;
        [SerializeField] private TextMeshProUGUI ctaLabel;

        [Header("Prefab images")]
        [SerializeField] private Image picture;
        [SerializeField] private Image logoImage;
        [SerializeField] private Image background;   // dark panel behind the neon frame

        [Header("Prefab buttons")]
        [SerializeField] private Button claimButton;

        [Header("Legacy")]
        [SerializeField] private bool usePlaceholderColor;

        private const int PictureTimeoutSeconds = 10;

        // Neon palette - matches the game art. The frame colour is the only per-offer accent.
        private static readonly Color NeonCyan = ParseHex("#00F0FF", new Color(0f, 0.94f, 1f));
        private static readonly Color NeonMagenta = ParseHex("#FF2BD6", new Color(1f, 0.17f, 0.84f));
        private static readonly Color PictureIdle = new Color(0.13f, 0.10f, 0.26f, 1f);

        private GeoAdOffer offer;
        private Button selfButton;

        private readonly System.Collections.Generic.Dictionary<Image, ImageSlot> slots =
            new System.Collections.Generic.Dictionary<Image, ImageSlot>();

        /// <summary>
        /// Fills the assigned labels/images from the offer. Creates nothing - every rect comes
        /// from the prefab so the whole card can be adjusted in the Inspector.
        /// An empty value in the offer keeps what was typed in the prefab.
        /// </summary>
        public void Bind(GeoAdOffer source)
        {
            offer = source;
            if (offer == null)
                return;

            ApplyText();
            HookButtons();
            ApplyFrameTheme();

            string productUrl = ResolveProductUrl(offer);
            if (picture != null)
                picture.color = string.IsNullOrEmpty(productUrl)
                    ? ParseColor(offer.placeholderColor, PictureIdle)
                    : Color.white;

            if (usePlaceholderColor && background != null)
                background.color = ParseColor(offer.placeholderColor, background.color);

            BindSlot(picture, productUrl);
            BindSlot(logoImage, offer.businessLogoUrl);
        }

        /// <summary>Code-built card: creates its own labels/button. Used only without a prefab.</summary>
        public void Build(Transform parent, GeoAdOffer source)
        {
            offer = source;

            var rect = GetComponent<RectTransform>();
            if (transform.parent != parent)
                transform.SetParent(parent, false);
            rect.sizeDelta = new Vector2(640, 61);

            var image = GetComponent<Image>();
            image.color = NeonCyan;

            titleLabel = CreateLabel("Title", 18, FontStyles.Bold, new Vector2(64, -4), new Vector2(-278, 23));
            descLabel = null;
            discountLabel = CreateLabel("Discount", 14, FontStyles.Bold, new Vector2(0, 0), new Vector2(168, 24));
            claimButton = CreateButton("Claim");

            ApplyText();
            HookButtons();
            ApplyFrameTheme();
        }

        private void OnEnable()
        {
            // a card bound while its carousel was hidden starts its downloads here
            foreach (var slot in slots.Values)
            {
                if (!string.IsNullOrEmpty(slot.pendingUrl) && slot.loadRoutine == null && isActiveAndEnabled)
                    slot.loadRoutine = StartCoroutine(LoadPicture(slot));
            }
        }

        private void OnDisable()
        {
            // deactivation stops coroutines; the requests must be released by hand
            foreach (var slot in slots.Values)
                CancelSlot(slot);
        }

        private void OnDestroy()
        {
            foreach (var slot in slots.Values)
            {
                CancelSlot(slot);
                ReleaseSlotPicture(slot);
            }
            slots.Clear();
        }

        /// <summary>Single source of truth for every text on the card.</summary>
        private void ApplyText()
        {
            if (offer == null)
                return;

            if (titleLabel != null)
                titleLabel.text = string.IsNullOrEmpty(offer.title) ? "(untitled offer)" : offer.title;

            if (descLabel != null && !string.IsNullOrEmpty(offer.description))
                descLabel.text = offer.description;

            if (discountLabel != null)
                discountLabel.text = offer.coupon != null && !string.IsNullOrEmpty(offer.coupon.discountText)
                    ? offer.coupon.discountText
                    : string.Empty;

            if (categoryLabel != null && !string.IsNullOrEmpty(offer.category))
                categoryLabel.text = offer.category;

            if (distanceLabel != null)
                distanceLabel.text = FormatDistance(offer.distanceKm);

            TextMeshProUGUI cta = CtaText();
            if (cta != null && !string.IsNullOrEmpty(offer.ctaText))
                cta.text = offer.ctaText;
        }

        /// <summary>
        /// The frame is the card's own Image: a neon rim around the inset dark panel.
        /// Affiliate/neutral offers glow cyan, merchant offers glow magenta.
        /// </summary>
        private void ApplyFrameTheme()
        {
            Image frame = GetComponent<Image>();
            if (frame == null)
                return;

            bool merchant = !string.IsNullOrEmpty(offer.type) && offer.type == "merchant_offer";
            Color neon = merchant ? NeonMagenta : NeonCyan;
            frame.color = neon;

            Outline[] glows = GetComponents<Outline>();
            for (int i = 0; i < glows.Length; i++)
                glows[i].effectColor = new Color(neon.r, neon.g, neon.b, i == 0 ? 0.8f : 0.45f);
        }

        private void HookButtons()
        {
            // Remove-then-add so a cloned card never ends up with the handler twice.
            if (claimButton != null)
            {
                claimButton.onClick.RemoveListener(OnClaimClicked);
                claimButton.onClick.AddListener(OnClaimClicked);
                if (ctaLabel == null)
                    ctaLabel = claimButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (selfButton == null)
                selfButton = GetComponent<Button>();
            if (selfButton != null)
            {
                selfButton.targetGraphic = GetComponent<Image>();
                selfButton.onClick.RemoveListener(OnClaimClicked);
                selfButton.onClick.AddListener(OnClaimClicked);
            }
        }

        private TextMeshProUGUI CtaText()
        {
            if (ctaLabel != null)
                return ctaLabel;
            if (claimButton != null)
                ctaLabel = claimButton.GetComponentInChildren<TextMeshProUGUI>(true);
            return ctaLabel;
        }

        private void OnClaimClicked()
        {
            if (GeoAdCarousel.InputLocked)
                return; // the pointer was dragging the carousel between pages
            if (offer == null)
                return;

            string code = offer.coupon != null && !string.IsNullOrEmpty(offer.coupon.code)
                ? offer.coupon.code
                : GeoCouponGenerator.Generate(string.IsNullOrEmpty(offer.type) ? "GEO" : offer.type);

            GUIUtility.systemCopyBuffer = code;

            if (!string.IsNullOrEmpty(offer.actionUrl))
                Application.OpenURL(offer.actionUrl);
            else if (offer.coupon != null)
                GeoCouponPopup.Show(code, offer); // offline/mock offers have no URL
        }

        /// <summary>0.06 km near by, 6.2 km further out - the geo proof on the card.</summary>
        internal static string FormatDistance(double km)
        {
            if (double.IsNaN(km) || km <= 0d)
                return "Nearby";
            if (km < 1d)
                return Mathf.RoundToInt((float)(km * 1000)) + " m";
            return km.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + " km";
        }

        internal static string ResolveProductUrl(GeoAdOffer source)
        {
            if (source == null)
                return null;
            if (!string.IsNullOrEmpty(source.productImageUrl))
                return source.productImageUrl;
            return source.imageUrl; // legacy fallback
        }

        private void BindSlot(Image target, string imageUrl)
        {
            if (target == null)
                return;

            if (!slots.TryGetValue(target, out ImageSlot slot) || slot == null)
            {
                slot = new ImageSlot { target = target, defaultSprite = target.sprite, defaultColor = target.color };
                slots[target] = slot;
            }

            if (string.IsNullOrEmpty(imageUrl))
            {
                // no image in the data: back to what the prefab had
                CancelSlot(slot);
                slot.pendingUrl = null;
                slot.loadedUrl = null;
                target.sprite = slot.defaultSprite;
                target.color = slot.defaultColor;
                ReleaseSlotPicture(slot);
                return;
            }

            if (imageUrl == slot.loadedUrl)
                return;

            CancelSlot(slot);
            slot.pendingUrl = imageUrl;
            if (isActiveAndEnabled && slot.loadRoutine == null)
                slot.loadRoutine = StartCoroutine(LoadPicture(slot));
        }

        private IEnumerator LoadPicture(ImageSlot slot)
        {
            string url = slot.pendingUrl;
            slot.request = UnityWebRequestTexture.GetTexture(url);
            slot.request.timeout = PictureTimeoutSeconds;
            yield return slot.request.SendWebRequest();

            UnityWebRequest request = slot.request;
            slot.request = null;
            slot.loadRoutine = null;
            slot.pendingUrl = null;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[GeoAds] picture download failed: " + request.error + " (" + url + ")");
                request.Dispose();
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            request.Dispose();
            if (texture == null || slot.target == null)
            {
                if (texture != null)
                    Destroy(texture);
                yield break;
            }

            ReleaseSlotPicture(slot);
            slot.texture = texture;
            slot.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            slot.target.sprite = slot.sprite;
            slot.target.color = Color.white; // drop the placeholder tint
            slot.loadedUrl = url;
        }

        private void CancelSlot(ImageSlot slot)
        {
            if (slot == null)
                return;
            if (slot.loadRoutine != null)
            {
                StopCoroutine(slot.loadRoutine);
                slot.loadRoutine = null;
            }
            if (slot.request != null)
            {
                slot.request.Dispose();
                slot.request = null;
            }
        }

        private void ReleaseSlotPicture(ImageSlot slot)
        {
            if (slot == null)
                return;
            if (slot.sprite != null)
                Destroy(slot.sprite);
            if (slot.texture != null)
                Destroy(slot.texture);
            slot.sprite = null;
            slot.texture = null;
        }

        private sealed class ImageSlot
        {
            public Image target;
            public Sprite defaultSprite;
            public Color defaultColor;
            public string pendingUrl;
            public string loadedUrl;
            public Coroutine loadRoutine;
            public UnityWebRequest request;
            public Texture2D texture;
            public Sprite sprite;
        }

        private TextMeshProUGUI CreateLabel(string name, int size, FontStyles style, Vector2 offset, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            // top-left: offset is the label's top-left corner, sizeDelta.x of a negative value
            // stretches it to a fixed distance from the card's right edge
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = sizeDelta;

            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.fontStyle = style;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = string.Empty;
            return label;
        }

        private Button CreateButton(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-64f, 0f);
            rect.sizeDelta = new Vector2(146f, -12f);
            var img = go.GetComponent<Image>();
            img.color = ParseHex("#00F0FF", new Color(0f, 0.94f, 1f));

            var label = new GameObject("Label", typeof(RectTransform));
            var lrect = (RectTransform)label.transform;
            lrect.SetParent(rect, false);
            lrect.anchorMin = Vector2.zero;
            lrect.anchorMax = Vector2.one;
            lrect.offsetMin = Vector2.zero;
            lrect.offsetMax = Vector2.zero;
            var text = label.AddComponent<TextMeshProUGUI>();
            text.text = "Claim";
            text.fontSize = 16f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.04f, 0.03f, 0.1f, 1f);
            text.raycastTarget = false;
            ctaLabel = text;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            return btn;
        }

        private static Color ParseColor(string hex, Color fallback)
        {
            return ParseHex(hex, fallback);
        }

        /// <summary>Accepts "FF00FF", "#FF00FF", "FF00FFFF" and "#FF00FFFF".</summary>
        private static Color ParseHex(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex))
                return fallback;
            string value = hex.Trim();
            if (value.Length == 0)
                return fallback;
            if (value[0] != '#')
                value = "#" + value;
            if (value.Length != 7 && value.Length != 9)
                return fallback;
            return ColorUtility.TryParseHtmlString(value, out Color c) ? c : fallback;
        }
    }

    /// <summary>Generates codes like GEO-MOCK-X8A21 (used when an offer carries no code).</summary>
    internal static class GeoCouponGenerator
    {
        public static string Generate(string prefix)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I/O/0/1
            var rng = new System.Random();
            char[] suffix = new char[5];
            for (int i = 0; i < suffix.Length; i++)
                suffix[i] = alphabet[rng.Next(alphabet.Length)];
            return (string.IsNullOrEmpty(prefix) ? "GEO" : prefix) + "-MOCK-" + new string(suffix);
        }
    }
}