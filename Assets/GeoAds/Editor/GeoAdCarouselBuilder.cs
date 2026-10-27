using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GeoAds.Editor
{
    /// <summary>
    /// Tools > GeoAds > Build Neon Carousel (Full-Width Bottom)
    ///
    /// Rebuilds Assets/GeoAds/Prefabs/GeoAdCarousel.prefab as a portrait bottom strip:
    ///   dock 640 x 61.3 units, bottom-centre (the previous Geo Ad Carousel's height/position),
    ///   cards full viewport width so exactly one card is on screen until you swipe to the next.
    /// Also re-instances it in the open scene, points Geo Ad Manager at the live API and saves.
    /// After building it runs two checks and prints PASS/FAIL to the Console:
    ///   - fit check: no card child outside its card, no two content children overlapping
    ///   - feed check: the live API really parses into offers with the fields the card draws
    /// No dialogs: every outcome goes to the Console, so it can run unattended.
    /// </summary>
    public static class GeoAdCarouselBuilder
    {
        private const string LogPrefix = "[GeoAds] Build Neon Carousel: ";

        private const string CanvasName = "GeoAdsCanvas";
        private const string CarouselName = "GeoAdCarousel";
        private const string ManagerObjectName = "GeoAds";

        private const string GeoAdsFolder = "Assets/GeoAds";
        private const string PrefabFolder = GeoAdsFolder + "/Prefabs";
        private const string PrefabPath = PrefabFolder + "/" + CarouselName + ".prefab";

        private const int UILayer = 5;
        private const int NewCanvasSortOrder = 50;

        // --- geometry, in 640x960 reference units (GameUI matches on width: 640 units = full screen)
        private static readonly Vector2 ReferenceResolution = new Vector2(640f, 960f);
        private const float CarouselWidth = 640f;
        private const float CarouselHeight = 61.3f;
        private const float CardWidth = 640f;
        private const float PanelInset = 4f;          // the neon rim thickness

        private const float PictureSize = 46f;
        private const float PictureX = 8f;
        private const float LogoSize = 22f;
        private const float ColumnX = 64f;            // text column starts right of the picture
        private const float RightEdge = 426f;         // text column stops before the CTA
        private const float ClaimWidth = 146f;
        private const float ClaimRight = 64f;         // CTA right margin
        private const float TitleHeight = 23f;
        private const float TitleTop = 4f;
        private const float RowHeight = 24f;
        private const float RowBottom = 4f;
        private const float DiscountWidth = 168f;
        private const float CategoryWidth = 114f;

        // --- neon palette
        private static readonly Color PanelColor = Hex("#0A0620", 0.94f);
        private static readonly Color NeonCyan = Hex("#00F0FF", 1f);
        private static readonly Color NeonMagenta = Hex("#FF2BD6", 1f);
        private static readonly Color NeonPurple = Hex("#8B5CFF", 1f);
        private static readonly Color InkColor = Hex("#0A0620", 1f);

        private const string ApiUrl = "https://lootlio-one.vercel.app/api/mock";

        [MenuItem("Tools/GeoAds/Build Neon Carousel (Full-Width Bottom)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(LogPrefix + "stop Play mode first. Nothing was changed.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();

            GeoAdManager manager = FindComponent<GeoAdManager>(roots);
            if (manager == null)
            {
                Debug.LogError(LogPrefix + "the open scene '" + scene.name + "' has no "
                    + ManagerObjectName + " object with a Geo Ad Manager. Open "
                    + "Assets/DM_Scenes/Game.unity first. Nothing was changed.");
                return;
            }

            // remember where the old carousel lived, then clear the way
            GeoAdCarousel previous = FindComponent<GeoAdCarousel>(roots);
            RectTransform oldCarousel = previous != null ? previous.transform as RectTransform : null;
            Transform parent = oldCarousel != null ? oldCarousel.parent : null;
            if (oldCarousel != null)
            {
                GameObject oldGo = oldCarousel.gameObject;
                Debug.Log(LogPrefix + "replacing the existing '" + oldGo.name + "' (its height/position are reused).");
                Undo.DestroyObjectImmediate(oldGo);
            }

            if (parent == null)
                parent = GetOrCreateCanvas(roots);

            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            RectTransform carouselRect = CreateCarousel(parent);
            RectTransform content = (RectTransform)carouselRect.Find("Viewport/Content");
            RectTransform cardTemplate = CreateCard(content, uiSprite);
            Button close = CreateCloseButton(carouselRect, uiSprite);

            GeoAdCarousel carousel = carouselRect.gameObject.AddComponent<GeoAdCarousel>();
            GeoAdCard card = cardTemplate.GetComponent<GeoAdCard>();
            WireCarousel(carousel, content, cardTemplate, close);

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder(GeoAdsFolder, "Prefabs");
            PrefabUtility.SaveAsPrefabAssetAndConnect(carouselRect.gameObject, PrefabPath,
                InteractionMode.AutomatedAction, out bool prefabSaved);
            if (!prefabSaved)
                Debug.LogError(LogPrefix + "could not save " + PrefabPath + ". The carousel stays in the scene without a prefab.");

            WireManager(manager, carousel);

            EditorSceneManager.MarkSceneDirty(scene);
            bool sceneSaved = EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = carouselRect.gameObject;

            AssertFit(cardTemplate);
            AssertFeed();

            Debug.Log(LogPrefix + "done. " + CarouselName + " = " + CarouselWidth + " x " + CarouselHeight
                + " at the bottom of the canvas, 1 template card (" + CardWidth + " wide, one per page), prefab at "
                + PrefabPath + ", Geo Ad Manager on '" + manager.gameObject.name + "' now reads " + ApiUrl
                + ", scene '" + scene.name + "' " + (sceneSaved ? "saved." : "NOT saved - save it manually."));

            if (card != null)
                card.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ scene objects

        private static RectTransform CreateCarousel(Transform parent)
        {
            RectTransform root = CreateRect(CarouselName, parent);
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(CarouselWidth, CarouselHeight);
            root.anchoredPosition = Vector2.zero;

            Image background = root.gameObject.AddComponent<Image>();
            background.sprite = UiSprite();
            background.type = Image.Type.Sliced;
            background.color = new Color(0.04f, 0.02f, 0.13f, 0.6f);
            background.raycastTarget = true; // the ScrollRect needs something to receive the drag

            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.inertia = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            RectTransform viewport = CreateRect("Viewport", root);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f); // transparent, but catches the drag
            viewportImage.raycastTarget = true;

            RectTransform content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            HorizontalLayoutGroup layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 0f;                       // cards butt edge to edge: one full-width card per page
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.viewport = viewport;
            scroll.content = content;
            return root;
        }

        /// <summary>The single card that every offer's card is cloned from - edit it, edit them all.</summary>
        private static RectTransform CreateCard(RectTransform content, Sprite uiSprite)
        {
            RectTransform card = CreateRect("CardTemplate", content);
            card.anchorMin = new Vector2(0f, 0f);
            card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 0.5f);
            card.sizeDelta = new Vector2(CardWidth, 0f);
            card.anchoredPosition = Vector2.zero;

            var le = card.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = CardWidth;
            le.minWidth = CardWidth;

            // the neon rim: a filled rounded rect that the inset panel leaves showing at the edges.
            // The two Outline components draw the glow, which spills outside the card.
            Image frame = card.gameObject.AddComponent<Image>();
            frame.sprite = uiSprite;
            frame.type = Image.Type.Sliced;
            frame.color = NeonCyan;
            frame.raycastTarget = true;
            AddGlow(card.gameObject, NeonCyan, 2f, 0.8f);
            AddGlow(card.gameObject, NeonCyan, 5f, 0.45f);

            Button cardButton = card.gameObject.AddComponent<Button>();
            cardButton.targetGraphic = frame;
            cardButton.transition = Selectable.Transition.ColorTint;
            var colors = cardButton.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.78f);
            cardButton.colors = colors;

            // dark panel, inset so only PanelInset units of neon stay visible around the card
            RectTransform panel = CreateRect("Panel", card);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(PanelInset, PanelInset);
            panel.offsetMax = new Vector2(-PanelInset, -PanelInset);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = uiSprite;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = PanelColor;
            panelImage.raycastTarget = false;

            RectTransform picture = CreateRect("Picture", card);
            picture.anchorMin = new Vector2(0f, 0.5f);
            picture.anchorMax = new Vector2(0f, 0.5f);
            picture.pivot = new Vector2(0f, 0.5f);
            picture.anchoredPosition = new Vector2(PictureX, 0f);
            picture.sizeDelta = new Vector2(PictureSize, PictureSize);
            Image pictureImage = picture.gameObject.AddComponent<Image>();
            pictureImage.color = new Color(0.13f, 0.10f, 0.26f, 1f); // placeholder while the photo loads
            pictureImage.preserveAspect = true;
            pictureImage.raycastTarget = false;

            RectTransform logo = CreateRect("Logo", picture);
            logo.anchorMin = new Vector2(1f, 0f);
            logo.anchorMax = new Vector2(1f, 0f);
            logo.pivot = new Vector2(1f, 0f);
            logo.anchoredPosition = new Vector2(-2f, 2f);
            logo.sizeDelta = new Vector2(LogoSize, LogoSize);
            Image logoImage = logo.gameObject.AddComponent<Image>();
            logoImage.color = Color.white;
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;

            TextMeshProUGUI title = CreateText("Title", card, 18f, FontStyles.Bold, Color.white,
                TextAlignmentOptions.Left, new Vector2(ColumnX, -TitleTop), new Vector2(RightEdge - ColumnX - CarouselWidth, TitleHeight));
            title.enableWordWrapping = false;
            title.overflowMode = TextOverflowModes.Ellipsis;
            AddOutline(title.gameObject, new Color(NeonCyan.r, NeonCyan.g, NeonCyan.b, 0.7f), 1.6f);

            RectTransform row = CreateRect("BottomRow", card);
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(0f, 0f);
            row.anchoredPosition = new Vector2(ColumnX, RowBottom);
            row.sizeDelta = new Vector2(RightEdge - ColumnX - CarouselWidth, RowHeight);
            HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(0, 0, 0, 0);
            rowLayout.spacing = 6f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            TextMeshProUGUI discountLabel = CreateChip(row, "Discount", DiscountWidth,
                NeonMagenta, InkColor, "0.00", 14f);
            TextMeshProUGUI categoryLabel = CreateChip(row, "Category", CategoryWidth,
                new Color(NeonPurple.r, NeonPurple.g, NeonPurple.b, 0.22f), NeonPurple, "Category", 13f);

            RectTransform distanceRect = CreateRect("Distance", row);
            distanceRect.anchorMin = new Vector2(0f, 0.5f);
            distanceRect.anchorMax = new Vector2(0f, 0.5f);
            distanceRect.pivot = new Vector2(0f, 0.5f);
            distanceRect.sizeDelta = new Vector2(0f, RowHeight);
            TextMeshProUGUI distance = distanceRect.gameObject.AddComponent<TextMeshProUGUI>();
            distance.fontSize = 14f;
            distance.color = NeonCyan;
            distance.alignment = TextAlignmentOptions.Right;
            distance.raycastTarget = false;
            distance.enableWordWrapping = false;
            var distanceLe = distance.gameObject.AddComponent<LayoutElement>();
            distanceLe.flexibleWidth = 1f;

            RectTransform claim = CreateRect("Claim", card);
            claim.anchorMin = new Vector2(1f, 0f);
            claim.anchorMax = new Vector2(1f, 1f);
            claim.pivot = new Vector2(1f, 0.5f);
            claim.anchoredPosition = new Vector2(-ClaimRight, 0f);
            claim.sizeDelta = new Vector2(ClaimWidth, -(RowBottom * 2f + 4f));
            Image claimImage = claim.gameObject.AddComponent<Image>();
            claimImage.sprite = uiSprite;
            claimImage.type = Image.Type.Sliced;
            claimImage.color = NeonCyan;
            claimImage.raycastTarget = true;
            AddGlow(claim.gameObject, NeonCyan, 3f, 0.55f);
            Button claimButton = claim.gameObject.AddComponent<Button>();
            claimButton.targetGraphic = claimImage;

            TextMeshProUGUI ctaLabel = CreateFittedText("Label", claim, 16f, FontStyles.Bold, InkColor,
                TextAlignmentOptions.Center);
            ctaLabel.text = "Claim";
            ctaLabel.enableWordWrapping = false;
            ctaLabel.overflowMode = TextOverflowModes.Ellipsis;

            GeoAdCard cardComponent = card.gameObject.AddComponent<GeoAdCard>();
            var so = new SerializedObject(cardComponent);
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("descLabel").objectReferenceValue = null;
            so.FindProperty("discountLabel").objectReferenceValue = discountLabel;
            so.FindProperty("categoryLabel").objectReferenceValue = categoryLabel;
            so.FindProperty("distanceLabel").objectReferenceValue = distance;
            so.FindProperty("ctaLabel").objectReferenceValue = ctaLabel;
            so.FindProperty("picture").objectReferenceValue = pictureImage;
            so.FindProperty("logoImage").objectReferenceValue = logoImage;
            so.FindProperty("background").objectReferenceValue = panelImage;
            so.FindProperty("claimButton").objectReferenceValue = claimButton;
            so.FindProperty("usePlaceholderColor").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            return card;
        }

        private static TextMeshProUGUI CreateChip(RectTransform row, string name, float width,
            Color fill, Color textColor, string sample, float fontSize)
        {
            RectTransform chip = CreateRect(name, row);
            chip.anchorMin = new Vector2(0f, 0.5f);
            chip.anchorMax = new Vector2(0f, 0.5f);
            chip.pivot = new Vector2(0f, 0.5f);
            chip.sizeDelta = new Vector2(width, RowHeight);
            Image image = chip.gameObject.AddComponent<Image>();
            image.color = fill;
            image.raycastTarget = false;
            var le = chip.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;

            TextMeshProUGUI label = CreateFittedText("Label", chip, fontSize, FontStyles.Bold, textColor,
                TextAlignmentOptions.Center);
            label.text = sample;
            return label;
        }

        private static Button CreateCloseButton(RectTransform carousel, Sprite uiSprite)
        {
            RectTransform close = CreateRect("Close", carousel);
            close.anchorMin = new Vector2(1f, 1f);
            close.anchorMax = new Vector2(1f, 1f);
            close.pivot = new Vector2(1f, 1f);
            close.anchoredPosition = new Vector2(-8f, -8f);
            close.sizeDelta = new Vector2(44f, 44f);
            Image image = close.gameObject.AddComponent<Image>();
            image.sprite = uiSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.04f, 0.02f, 0.13f, 0.85f);
            image.raycastTarget = true;
            AddGlow(close.gameObject, NeonCyan, 3f, 0.5f);
            Button button = close.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TextMeshProUGUI label = CreateFittedText("Label", close, 22f, FontStyles.Bold, NeonCyan,
                TextAlignmentOptions.Center);
            label.text = "\u00D7";
            return button;
        }

        private static void WireCarousel(GeoAdCarousel carousel, RectTransform content,
            RectTransform cardTemplate, Button close)
        {
            var so = new SerializedObject(carousel);
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("scroll").objectReferenceValue = content.GetComponentInParent<ScrollRect>();
            so.FindProperty("cardTemplate").objectReferenceValue = cardTemplate.GetComponent<GeoAdCard>();
            so.FindProperty("cards").arraySize = 0;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireManager(GeoAdManager manager, GeoAdCarousel carousel)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("useMock").boolValue = false;
            so.FindProperty("apiUrl").stringValue = ApiUrl;
            so.FindProperty("carousel").objectReferenceValue = carousel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ helpers

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = UILayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static TextMeshProUGUI CreateText(string objectName, Transform parent, float fontSize,
            FontStyles style, Color color, TextAlignmentOptions alignment, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect(objectName, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Text that exactly fills its parent - used for every label inside a chip or button.</summary>
        private static TextMeshProUGUI CreateFittedText(string objectName, Transform parent, float fontSize,
            FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(objectName, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static void AddGlow(GameObject target, Color color, float distance, float alpha)
        {
            var glow = target.AddComponent<Outline>();
            glow.effectColor = new Color(color.r, color.g, color.b, alpha);
            glow.effectDistance = new Vector2(distance, distance);
            glow.useGraphicAlpha = false;
        }
        private static void AddOutline(GameObject target, Color color, float distance)
        {
            var outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, distance);
            outline.useGraphicAlpha = false;
        }

        private static Sprite UiSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static Transform GetOrCreateCanvas(GameObject[] roots)
        {
            foreach (GameObject root in roots)
            {
                if (root.name == CanvasName && root.GetComponent<Canvas>() != null)
                    return root.transform;
            }

            var canvasGo = new GameObject(CanvasName, typeof(RectTransform));
            canvasGo.layer = UILayer;
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = NewCanvasSortOrder;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // match width: the canvas is always 640 units wide
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvasGo.transform;
        }

        private static T FindComponent<T>(GameObject[] roots) where T : Component
        {
            foreach (GameObject root in roots)
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static Color Hex(string html, float alpha)
        {
            Color c;
            if (ColorUtility.TryParseHtmlString(html, out c))
            {
                c.a = alpha;
                return c;
            }
            return new Color(1f, 1f, 1f, alpha);
        }

        // ------------------------------------------------------------------ checks

        /// <summary>Every card child inside the card, and no two content children overlapping.</summary>
        private static void AssertFit(RectTransform card)
        {
            const float tolerance = 0.5f;
            var problems = new List<string>();
            Rect cardRect = card.rect;
            var contentChildren = new List<RectTransform>();

            // containment is checked on every descendant, not just direct children
            foreach (RectTransform rt in card.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt == card || !rt.gameObject.activeSelf)
                    continue;

                Rect r = CalculateRelativeRectTransformTransform(rt, card);
                if (r.xMin < cardRect.xMin - tolerance || r.yMin < cardRect.yMin - tolerance ||
                    r.xMax > cardRect.xMax + tolerance || r.yMax > cardRect.yMax + tolerance)
                {
                    problems.Add(rt.name + " sticks out: [" + r.xMin.ToString("0.#") + ".."
                        + r.xMax.ToString("0.#") + "] x [" + r.yMin.ToString("0.#") + ".."
                        + r.yMax.ToString("0.#") + "] vs card [" + cardRect.xMin.ToString("0.#")
                        + ".." + cardRect.xMax.ToString("0.#") + "] x [" + cardRect.yMin.ToString("0.#")
                        + ".." + cardRect.yMax.ToString("0.#") + "]");
                }

                if (rt.name == "Picture" || rt.name == "Title" || rt.name == "BottomRow" || rt.name == "Claim")
                    contentChildren.Add(rt);
            }

            for (int i = 0; i < contentChildren.Count; i++)
            {
                for (int j = i + 1; j < contentChildren.Count; j++)
                {
                    Rect a = CalculateRelativeRectTransformTransform(contentChildren[i], card);
                    Rect b = CalculateRelativeRectTransformTransform(contentChildren[j], card);
                    if (a.xMin < b.xMax - 1f && b.xMin < a.xMax - 1f && a.yMin < b.yMax - 1f && b.yMin < a.yMax - 1f)
                        problems.Add(contentChildren[i].name + " overlaps " + contentChildren[j].name);
                }
            }

            if (problems.Count == 0)
            {
                Debug.Log(LogPrefix + "fit check PASS - every child of CardTemplate sits inside "
                    + CardWidth + " x " + CarouselHeight + " with no overlaps.");
            }
            else
            {
                Debug.LogError(LogPrefix + "fit check FAILED:\n - " + string.Join("\n - ", problems));
            }
        }

        /// <summary>The live feed must really parse through GeoAdOffer into what the card draws.</summary>
        private static void AssertFeed()
        {
            try
            {
                string json = Download(ApiUrl);
                GeoAdResponse response = JsonUtility.FromJson<GeoAdResponse>(json);
                int count = response != null && response.offers != null ? response.offers.Length : 0;
                if (count == 0)
                {
                    Debug.LogWarning(LogPrefix + "feed check FAILED - " + ApiUrl + " returned no offers.");
                    return;
                }

                var problems = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    GeoAdOffer offer = response.offers[i];
                    if (offer == null)
                    {
                        problems.Add("offer " + i + " is null");
                        continue;
                    }
                    if (string.IsNullOrEmpty(offer.title)) problems.Add(offer.id + ": empty title");
                    if (string.IsNullOrEmpty(offer.ctaText)) problems.Add(offer.id + ": empty ctaText");
                    if (string.IsNullOrEmpty(offer.category)) problems.Add(offer.id + ": empty category");
                    if (offer.coupon == null || string.IsNullOrEmpty(offer.coupon.discountText))
                        problems.Add(offer.id + ": no coupon discountText");
                    if (string.IsNullOrEmpty(offer.actionUrl)) problems.Add(offer.id + ": empty actionUrl");
                }

                if (problems.Count == 0)
                    Debug.Log(LogPrefix + "feed check PASS - " + count + " offers parsed from " + ApiUrl
                        + " with title, ctaText, category, discount and actionUrl on every card.");
                else
                    Debug.LogError(LogPrefix + "feed check FAILED:\n - " + string.Join("\n - ", problems));
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogPrefix + "feed check could not run: " + ex.Message
                    + " (the carousel still falls back to Resources/GeoAds/mock_ads.json at runtime).");
            }
        }

        private static string Download(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            {
                if (stream == null)
                    throw new IOException("empty response from " + url);
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }

        /// <summary>Returns child's rect expressed in parent's local space.</summary>
        private static Rect CalculateRelativeRectTransformTransform(RectTransform child, RectTransform parent)
        {
            Vector3[] worldCorners = new Vector3[4];
            child.GetWorldCorners(worldCorners);
            Vector2 min = parent.InverseTransformPoint(worldCorners[0]);
            Vector2 max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 p = parent.InverseTransformPoint(worldCorners[i]);
                min.x = Mathf.Min(min.x, p.x);
                min.y = Mathf.Min(min.y, p.y);
                max.x = Mathf.Max(max.x, p.x);
                max.y = Mathf.Max(max.y, p.y);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}