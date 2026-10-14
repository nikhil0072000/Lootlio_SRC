using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GeoAds
{
    /// <summary>
    /// Modal coupon popup: shows the claimed code, a mock copy button, QR placeholder
    /// text, and a close button. Fully runtime-built; no scene or prefab dependency.
    /// </summary>
    public class GeoCouponPopup : MonoBehaviour
    {
        private const int SortOrder = 950;

        private static GeoCouponPopup activePopup;

        public static void Show(string couponCode, GeoAdOffer context)
        {
            if (activePopup != null)
            {
                activePopup.Refresh(couponCode, context);
                return;
            }

            var host = new GameObject("GeoCouponPopup", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(GeoCouponPopup));
            activePopup = host.GetComponent<GeoCouponPopup>();
            activePopup.Build(couponCode, context);
        }

        public static void Close()
        {
            if (activePopup != null)
                Destroy(activePopup.gameObject);
            activePopup = null;
        }

        private TextMeshProUGUI codeLabel;

        private void Build(string couponCode, GeoAdOffer context)
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortOrder;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640, 960);
            scaler.matchWidthOrHeight = 1f;

            var dimmer = new GameObject("Dimmer", typeof(RectTransform), typeof(Image), typeof(Button));
            var drect = (RectTransform)dimmer.transform;
            drect.SetParent(transform, false);
            drect.anchorMin = Vector2.zero;
            drect.anchorMax = Vector2.one;
            drect.offsetMin = Vector2.zero;
            drect.offsetMax = Vector2.zero;
            var dimImage = dimmer.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.7f);
            dimmer.GetComponent<Button>().onClick.AddListener(Close);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var prect = (RectTransform)panel.transform;
            prect.SetParent(transform, false);
            prect.sizeDelta = new Vector2(480, 520);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.22f);

            CreateLabel(prect, "Title", "Coupon Claimed!", 40, FontStyles.Bold, new Vector2(0, 190));
            CreateLabel(prect, "Store", context != null && !string.IsNullOrEmpty(context.storeName) ? context.storeName : "Partner store", 28, FontStyles.Normal, new Vector2(0, 130));

            codeLabel = CreateLabel(prect, "Code", "-", 46, FontStyles.Bold, new Vector2(0, 40));
            codeLabel.color = new Color(1f, 0.85f, 0.2f);

            var copyBtn = CreateButton(prect, "Copy", new Vector2(0, -60));
            copyBtn.onClick.AddListener(() => MockCopy(codeLabel.text));

            var qrLabel = CreateLabel(prect, "QRPlaceholder", "[ QR placeholder ]", 24, FontStyles.Italic, new Vector2(0, -150));
            qrLabel.color = new Color(1f, 1f, 1f, 0.5f);

            var closeBtn = CreateButton(prect, "Close", new Vector2(0, -220));
            closeBtn.onClick.AddListener(Close);

            Refresh(couponCode, context);
        }

        private void Refresh(string couponCode, GeoAdOffer context)
        {
            if (codeLabel != null && !string.IsNullOrEmpty(couponCode))
                codeLabel.text = couponCode;
        }

        private void MockCopy(string text)
        {
            // MOCK COPY for now: real clipboard support is a future decision.
            Debug.Log("[GeoAds] mock-copy: " + text);
        }

        private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, string text, int size, FontStyles style, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            // stretch across the panel: -40 width = 20 px margin on each side
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(-40, 70);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.fontStyle = style;
            label.color = Color.white;
            label.text = text;
            return label;
        }

        private static Button CreateButton(RectTransform parent, string name, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(220, 70);
            go.GetComponent<Image>().color = new Color(0.95f, 0.6f, 0.1f);
            var labelGo = new GameObject("Label", typeof(RectTransform));
            var lrect = (RectTransform)labelGo.transform;
            lrect.SetParent(rect, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = name;
            label.fontSize = 30;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = Vector2.zero;
            return go.GetComponent<Button>();
        }
    }
}
