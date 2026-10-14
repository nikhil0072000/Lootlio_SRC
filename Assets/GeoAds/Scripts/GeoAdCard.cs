using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace GeoAds
{
    /// <summary>
    /// One coupon/ad card. Two ways to use it:
    /// - hand-made: the card and its children already exist (scene/prefab) and are
    ///   assigned in the Inspector; Bind() only fills them, nothing is created.
    /// - code-built: Build() creates the visuals at runtime (no prefab needed),
    ///   placeholder color block instead of a remote image.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class GeoAdCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI descLabel;
        [SerializeField] private TextMeshProUGUI discountLabel;
        [SerializeField] private Image picture;
        [SerializeField] private Image logoImage;
        [SerializeField] private Image background;
        [SerializeField] private Button claimButton;
        [SerializeField] private bool usePlaceholderColor;

        private const int PictureTimeoutSeconds = 10;

        private GeoAdOffer offer;
        private bool claimHooked;

        private readonly System.Collections.Generic.Dictionary<Image, ImageSlot> slots =
            new System.Collections.Generic.Dictionary<Image, ImageSlot>();

        /// <summary>
        /// Hand-made card: fills the assigned labels/picture from the offer. Creates nothing.
        /// An empty value in the offer keeps what was typed in the prefab.
        /// </summary>
        public void Bind(GeoAdOffer source)
        {
            offer = source;
            if (offer == null)
                return;

            if (titleLabel != null && !string.IsNullOrEmpty(offer.title))
                titleLabel.text = offer.title;
            if (descLabel != null && !string.IsNullOrEmpty(offer.description))
                descLabel.text = offer.description;
            string discount = offer.coupon != null ? offer.coupon.discountText : null;
            if (discountLabel != null && !string.IsNullOrEmpty(discount))
                discountLabel.text = discount;

            if (claimButton != null && !claimHooked)
            {
                claimButton.onClick.AddListener(OnClaimClicked);
                claimHooked = true;
            }

            if (usePlaceholderColor && background != null)
                background.color = ParseColor(offer.placeholderColor, background.color);

            BindSlot(picture, ResolveProductUrl(offer));
            BindSlot(logoImage, offer.businessLogoUrl);
        }

        public void Build(Transform parent, GeoAdOffer source)
        {
            offer = source;

            var rect = GetComponent<RectTransform>();
            if (transform.parent != parent)
                transform.SetParent(parent, false);
            rect.sizeDelta = new Vector2(420, 260);

            var image = GetComponent<Image>();
            image.color = ParseColor(offer != null ? offer.placeholderColor : null, new Color(0.25f, 0.5f, 0.85f));

            titleLabel = CreateLabel("Title", 34, FontStyles.Bold, new Vector2(0, -20), new Vector2(-40, 80));
            descLabel = CreateLabel("Desc", 26, FontStyles.Normal, new Vector2(0, -105), new Vector2(-40, 80));
            discountLabel = CreateLabel("Discount", 30, FontStyles.Bold, new Vector2(0, -160), new Vector2(-40, 60));
            claimButton = CreateButton("Claim");
            claimHooked = true;

            Refresh();
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

        private void Refresh()
        {
            if (offer == null)
                return;
            if (titleLabel != null)
                titleLabel.text = string.IsNullOrEmpty(offer.title) ? "(untitled offer)" : offer.title;
            if (descLabel != null)
                descLabel.text = string.IsNullOrEmpty(offer.description)
                    ? (offer.coupon != null ? offer.coupon.discountText : "Nearby deal")
                    : offer.description;
            if (discountLabel != null)
                discountLabel.text = offer.coupon != null ? (offer.coupon.discountText ?? string.Empty) : string.Empty;
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
                slot = new ImageSlot { target = target, defaultSprite = target.sprite };
                slots[target] = slot;
            }

            if (string.IsNullOrEmpty(imageUrl))
            {
                // no image in the data: back to the picture set in the prefab
                CancelSlot(slot);
                slot.pendingUrl = null;
                slot.loadedUrl = null;
                target.sprite = slot.defaultSprite;
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
            // top-stretch: offset.y is the distance below the card's top edge,
            // sizeDelta.x of -40 leaves a 20 px margin on each side
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = sizeDelta;

            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.fontStyle = style;
            label.color = Color.white;
            label.text = string.Empty;
            return label;
        }

        private Button CreateButton(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0, 40);
            rect.sizeDelta = new Vector2(200, 64);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.95f, 0.6f, 0.1f);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(OnClaimClicked);
            return btn;
        }

        private void OnClaimClicked()
        {
            if (offer == null || offer.coupon == null)
                return;

            // Mock claim flow: generate a fake code like MOCK-MOCK-X8A21.
            string code = string.IsNullOrEmpty(offer.coupon.code)
                ? GeoCouponGenerator.Generate("MOCK")
                : offer.coupon.code;

            GeoCouponPopup.Show(code, offer);
        }

        private static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length < 6)
                return fallback;
            if (ColorUtility.TryParseHtmlString("#" + hex, out Color c))
                return c;
            return fallback;
        }
    }

    /// <summary>Generates codes like MOCK50-X8A21 (mock claim flow).</summary>
    internal static class GeoCouponGenerator
    {
        public static string Generate(string prefix)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I/O/0/1
            var rng = new System.Random();
            char[] suffix = new char[5];
            for (int i = 0; i < suffix.Length; i++)
                suffix[i] = alphabet[rng.Next(alphabet.Length)];
            return prefix + "-MOCK-" + new string(suffix);
        }
    }
}
