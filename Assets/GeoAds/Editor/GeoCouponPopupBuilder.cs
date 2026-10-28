using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GeoAds.Editor
{
    /// <summary>
    /// Tools > GeoAds > Build Coupon Popup Prefab
    ///
    /// Creates Assets/GeoAds/Prefabs/GeoCouponPopup.prefab: a normal uGUI + TextMeshPro popup that
    /// can be restyled freely in the editor (panel, colours, fonts, texts, button sizes/positions).
    /// Then assigns it to "Coupon Popup Prefab" on Assets/GeoAds/Prefabs/GeoAdCarousel.prefab.
    /// An existing popup prefab is only replaced after you confirm (never in batch mode), so your
    /// edits are safe; the carousel link is refreshed either way.
    /// </summary>
    public static class GeoCouponPopupBuilder
    {
        private const string LogPrefix = "[GeoAds] ";
        private const string PrefabFolder = "Assets/GeoAds/Prefabs";
        private const string PopupPath = PrefabFolder + "/GeoCouponPopup.prefab";
        private const string CarouselPath = PrefabFolder + "/GeoAdCarousel.prefab";

        private const int UILayer = 5;
        private const int SortOrder = 950; // above the carousel (900)

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color PanelColor = Hex("#0A0620", 0.97f);
        private static readonly Color CodeBoxColor = Hex("#1B1240", 1f);
        private static readonly Color NeonCyan = Hex("#00F0FF", 1f);
        private static readonly Color NeonMagenta = Hex("#FF2BD6", 1f);
        private static readonly Color CodeColor = Hex("#FFE14D", 1f);
        private static readonly Color InkColor = Hex("#0A0620", 1f);
        private static readonly Color CloseColor = Hex("#3A2F66", 1f);

        [MenuItem("Tools/GeoAds/Build Coupon Popup Prefab")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(LogPrefix + "stop Play mode first. Nothing was changed.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/GeoAds", "Prefabs");

            GeoCouponPopup popup = AssetDatabase.LoadAssetAtPath<GeoCouponPopup>(PopupPath);
            bool build = popup == null;
            if (!build && !Application.isBatchMode)
            {
                build = EditorUtility.DisplayDialog("Coupon popup prefab",
                    PopupPath + " already exists.\n\nReplace it with the default design? Your changes to it will be lost.",
                    "Replace", "Keep mine");
            }

            if (build)
            {
                popup = CreatePrefab();
                if (popup == null)
                {
                    Debug.LogError(LogPrefix + "could not save " + PopupPath + ". Nothing was changed.");
                    return;
                }
                Debug.Log(LogPrefix + "Coupon popup prefab saved: " + PopupPath);
            }
            else
            {
                Debug.Log(LogPrefix + "kept the existing " + PopupPath + ".");
            }

            AssignToCarousel(popup);
            AssetDatabase.SaveAssets();
            Selection.activeObject = popup != null ? popup.gameObject : null;
        }

        // ------------------------------------------------------------------ prefab

        private static GeoCouponPopup CreatePrefab()
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var root = new GameObject("GeoCouponPopup", typeof(RectTransform));
            root.layer = UILayer;
            try
            {
                Canvas canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = SortOrder;
                CanvasScaler scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(640f, 960f);
                scaler.matchWidthOrHeight = 0.5f;
                root.AddComponent<GraphicRaycaster>();

                // dark full-screen layer; tapping it closes the popup
                RectTransform dimmer = CreateRect("Dimmer", root.transform);
                Stretch(dimmer);
                Image dimImage = dimmer.gameObject.AddComponent<Image>();
                dimImage.color = DimColor;
                Button dimmerButton = dimmer.gameObject.AddComponent<Button>();
                dimmerButton.transition = Selectable.Transition.None;
                dimmerButton.targetGraphic = dimImage;

                // the panel (also a raycast target, so taps on it do not reach the dimmer)
                RectTransform panel = CreateRect("Panel", root.transform);
                panel.sizeDelta = new Vector2(520f, 460f);
                Image panelImage = panel.gameObject.AddComponent<Image>();
                panelImage.sprite = uiSprite;
                panelImage.type = Image.Type.Sliced;
                panelImage.color = PanelColor;
                Outline glow = panel.gameObject.AddComponent<Outline>();
                glow.effectColor = new Color(NeonCyan.r, NeonCyan.g, NeonCyan.b, 0.85f);
                glow.effectDistance = new Vector2(3f, -3f);

                CreateLabel(panel, "Title", "Coupon Claimed!", 36, FontStyles.Bold, NeonCyan, new Vector2(0f, 180f), 56f);
                TextMeshProUGUI store = CreateLabel(panel, "Store", "Partner store", 26, FontStyles.Normal, Color.white, new Vector2(0f, 125f), 40f);
                TextMeshProUGUI discount = CreateLabel(panel, "Discount", "Special offer", 24, FontStyles.Bold, NeonMagenta, new Vector2(0f, 82f), 36f);

                RectTransform codeBox = CreateRect("CodeBox", panel);
                codeBox.anchoredPosition = new Vector2(0f, 5f);
                codeBox.sizeDelta = new Vector2(400f, 86f);
                Image codeBoxImage = codeBox.gameObject.AddComponent<Image>();
                codeBoxImage.sprite = uiSprite;
                codeBoxImage.type = Image.Type.Sliced;
                codeBoxImage.color = CodeBoxColor;
                codeBoxImage.raycastTarget = false;
                Outline codeRim = codeBox.gameObject.AddComponent<Outline>();
                codeRim.effectColor = new Color(NeonMagenta.r, NeonMagenta.g, NeonMagenta.b, 0.8f);
                codeRim.effectDistance = new Vector2(2f, -2f);
                TextMeshProUGUI code = CreateLabel(codeBox, "Code", "CODE", 44, FontStyles.Bold, CodeColor, Vector2.zero, 0f);
                Stretch(code.rectTransform);

                TextMeshProUGUI copied = CreateLabel(panel, "Copied", "Copied!", 22, FontStyles.Bold, NeonCyan, new Vector2(0f, -65f), 32f);
                copied.gameObject.SetActive(false);

                Button copyButton = CreateButton(panel, uiSprite, "CopyButton", "Copy", NeonCyan, InkColor, new Vector2(-115f, -150f));
                Button closeButton = CreateButton(panel, uiSprite, "CloseButton", "Close", CloseColor, Color.white, new Vector2(115f, -150f));

                GeoCouponPopup popup = root.AddComponent<GeoCouponPopup>();
                var so = new SerializedObject(popup);
                so.FindProperty("storeLabel").objectReferenceValue = store;
                so.FindProperty("discountLabel").objectReferenceValue = discount;
                so.FindProperty("codeLabel").objectReferenceValue = code;
                so.FindProperty("copyButton").objectReferenceValue = copyButton;
                so.FindProperty("closeButton").objectReferenceValue = closeButton;
                so.FindProperty("dimmerButton").objectReferenceValue = dimmerButton;
                so.FindProperty("copiedFeedback").objectReferenceValue = copied.gameObject;
                so.FindProperty("copiedSeconds").floatValue = 1.5f;
                so.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PopupPath, out bool success);
                return success && saved != null ? saved.GetComponent<GeoCouponPopup>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssignToCarousel(GeoCouponPopup popup)
        {
            if (popup == null)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CarouselPath) == null)
            {
                Debug.LogWarning(LogPrefix + CarouselPath + " not found. Drag GeoCouponPopup into "
                    + "Geo Ad Carousel > Coupon Popup Prefab on your carousel by hand.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(CarouselPath);
            try
            {
                GeoAdCarousel carousel = contents.GetComponentInChildren<GeoAdCarousel>(true);
                if (carousel == null)
                {
                    Debug.LogWarning(LogPrefix + CarouselPath + " has no Geo Ad Carousel component. Popup not assigned.");
                    return;
                }

                var so = new SerializedObject(carousel);
                so.FindProperty("couponPopupPrefab").objectReferenceValue = popup;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, CarouselPath);
                Debug.Log(LogPrefix + "Coupon popup assigned to " + CarouselPath + " (Coupon Popup Prefab).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>A label stretched across its parent (20-unit side margins) at a fixed height.</summary>
        private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, string text, float size,
            FontStyles style, Color color, Vector2 position, float height)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(-40f, height);

            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(RectTransform parent, Sprite sprite, string name, string caption,
            Color fill, Color textColor, Vector2 position)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(200f, 64f);

            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = fill;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TextMeshProUGUI label = CreateLabel(rect, "Label", caption, 28, FontStyles.Bold, textColor, Vector2.zero, 0f);
            Stretch(label.rectTransform);
            return button;
        }

        private static Color Hex(string hex, float alpha)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color c))
                c = Color.white;
            c.a = alpha;
            return c;
        }
    }
}
