using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GeoAds.Editor
{
    /// <summary>
    /// Tools > GeoAds > Build Carousel In Open Scene
    /// Builds the hand-made style GeoAdCarousel (scroll view, cards, close button) in the
    /// open scene, saves it as a prefab and links it to Geo Ad Manager.
    /// No dialogs: every outcome goes to the Console, so it can be run unattended.
    /// Never replaces an existing GeoAdCarousel object or prefab.
    /// </summary>
    public static class GeoAdCarouselBuilder
    {
        private const string LogPrefix = "[GeoAds] Build Carousel: ";

        private const string CanvasName = "GeoAdsCanvas";
        private const string CarouselName = "GeoAdCarousel";

        private const string GeoAdsFolder = "Assets/GeoAds";
        private const string PrefabFolder = GeoAdsFolder + "/Prefabs";
        private const string PrefabPath = PrefabFolder + "/" + CarouselName + ".prefab";
        private const string SpriteFolder = "Assets/Em_Sprites";

        private const int CardCount = 5;
        private const int UILayer = 5;
        private const int CanvasSortOrder = 50;

        // Same reference resolution as the main gameplay canvas.
        private static readonly Vector2 ReferenceResolution = new Vector2(640f, 960f);
        private static readonly Vector2 CarouselSize = new Vector2(600f, 300f);
        private static readonly Vector2 CardSize = new Vector2(420f, 260f);

        private const float CarouselBottomOffset = 20f;
        private const float ContentSpacing = 40f;
        private const int ContentPadding = 20;

        [MenuItem("Tools/GeoAds/Build Carousel In Open Scene")]
        public static void BuildInOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(LogPrefix + "stop Play mode first. Nothing was changed.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();

            GeoAdManager manager = FindManager(roots);
            if (manager == null)
            {
                Debug.LogError(LogPrefix + "the open scene '" + scene.name + "' has no Geo Ad Manager. Nothing was changed.");
                return;
            }
            if (FindByName(roots, CarouselName) != null)
            {
                Debug.LogError(LogPrefix + "an object named " + CarouselName + " already exists in the scene. Delete it first to rebuild. Nothing was changed.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.LogError(LogPrefix + PrefabPath + " already exists. Delete it first to rebuild. Nothing was changed.");
                return;
            }

            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            List<Sprite> pictures = CollectSprites();

            RectTransform canvas = GetOrCreateCanvas(roots);
            RectTransform carouselRect = CreateCarousel(canvas, out RectTransform content);

            var cards = new GeoAdCard[CardCount];
            for (int i = 0; i < CardCount; i++)
            {
                Sprite picture = i < pictures.Count ? pictures[i] : null;
                cards[i] = CreateCard(content, i + 1, picture, uiSprite);
            }

            Button close = CreateButton("Close", "X", carouselRect, uiSprite, 26f);
            RectTransform closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = Vector2.one;
            closeRect.anchorMax = Vector2.one;
            closeRect.pivot = Vector2.one;
            closeRect.sizeDelta = new Vector2(44f, 44f);
            closeRect.anchoredPosition = new Vector2(-4f, -4f);

            GeoAdCarousel carousel = carouselRect.gameObject.AddComponent<GeoAdCarousel>();
            var carouselObject = new SerializedObject(carousel);
            SerializedProperty cardsProperty = carouselObject.FindProperty("cards");
            cardsProperty.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
                cardsProperty.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            carouselObject.FindProperty("closeButton").objectReferenceValue = close;
            carouselObject.ApplyModifiedPropertiesWithoutUndo();

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder(GeoAdsFolder, "Prefabs");
            PrefabUtility.SaveAsPrefabAssetAndConnect(carouselRect.gameObject, PrefabPath, InteractionMode.AutomatedAction, out bool prefabSaved);
            if (!prefabSaved)
                Debug.LogError(LogPrefix + "could not save " + PrefabPath + ". The carousel stays in the scene without a prefab.");

            var managerObject = new SerializedObject(manager);
            managerObject.FindProperty("carousel").objectReferenceValue = carousel;
            managerObject.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError(LogPrefix + "could not save the scene '" + scene.name + "'.");
                return;
            }

            Selection.activeGameObject = carouselRect.gameObject;
            Debug.Log(LogPrefix + "done. " + CanvasName + " > " + CarouselName + " with " + cards.Length + " cards ("
                + Mathf.Min(pictures.Count, CardCount) + " with a picture), prefab at " + PrefabPath
                + ", linked to Geo Ad Manager on '" + manager.gameObject.name + "', scene '" + scene.name + "' saved.");
        }

        private static GeoAdManager FindManager(GameObject[] roots)
        {
            foreach (GameObject root in roots)
            {
                GeoAdManager manager = root.GetComponentInChildren<GeoAdManager>(true);
                if (manager != null)
                    return manager;
            }
            return null;
        }

        private static Transform FindByName(GameObject[] roots, string objectName)
        {
            foreach (GameObject root in roots)
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == objectName)
                        return child;
                }
            }
            return null;
        }

        private static RectTransform GetOrCreateCanvas(GameObject[] roots)
        {
            foreach (GameObject root in roots)
            {
                if (root.name == CanvasName && root.GetComponent<Canvas>() != null)
                    return (RectTransform)root.transform;
            }

            var canvasGo = new GameObject(CanvasName, typeof(RectTransform));
            canvasGo.layer = UILayer;
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();
            return (RectTransform)canvasGo.transform;
        }

        private static RectTransform CreateCarousel(RectTransform canvas, out RectTransform content)
        {
            // bottom-centre of the screen
            RectTransform root = CreateRect(CarouselName, canvas);
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = CarouselSize;
            root.anchoredPosition = new Vector2(0f, CarouselBottomOffset);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.5f);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = CreateRect("Viewport", root);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();
            // transparent image so drags anywhere in the viewport reach the ScrollRect
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);

            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            HorizontalLayoutGroup layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(ContentPadding, ContentPadding, ContentPadding, ContentPadding);
            layout.spacing = ContentSpacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            return root;
        }

        private static GeoAdCard CreateCard(RectTransform content, int number, Sprite pictureSprite, Sprite uiSprite)
        {
            RectTransform cardRect = CreateRect("GeoAdCard_" + number, content);
            cardRect.sizeDelta = CardSize;
            Image background = cardRect.gameObject.AddComponent<Image>();
            background.color = new Color(0.16f, 0.36f, 0.62f, 1f);

            RectTransform pictureRect = CreateRect("Picture", cardRect);
            pictureRect.anchorMin = new Vector2(0f, 0.5f);
            pictureRect.anchorMax = new Vector2(0f, 0.5f);
            pictureRect.pivot = new Vector2(0f, 0.5f);
            pictureRect.sizeDelta = new Vector2(150f, 150f);
            pictureRect.anchoredPosition = new Vector2(20f, 25f);
            Image picture = pictureRect.gameObject.AddComponent<Image>();
            picture.sprite = pictureSprite;
            picture.preserveAspect = true;
            picture.raycastTarget = false;

            TextMeshProUGUI title = CreateLabel("Title", "Offer title " + number, cardRect, 28f, FontStyles.Bold,
                new Vector2(190f, -20f), new Vector2(210f, 70f));
            TextMeshProUGUI desc = CreateLabel("Desc", "Short description of offer " + number + ".", cardRect, 20f, FontStyles.Normal,
                new Vector2(190f, -95f), new Vector2(210f, 85f));

            Button claim = CreateButton("Claim", "Claim", cardRect, uiSprite, 24f);
            RectTransform claimRect = (RectTransform)claim.transform;
            claimRect.anchorMin = new Vector2(1f, 0f);
            claimRect.anchorMax = new Vector2(1f, 0f);
            claimRect.pivot = new Vector2(1f, 0f);
            claimRect.sizeDelta = new Vector2(160f, 50f);
            claimRect.anchoredPosition = new Vector2(-20f, 20f);

            GeoAdCard card = cardRect.gameObject.AddComponent<GeoAdCard>();
            var cardObject = new SerializedObject(card);
            cardObject.FindProperty("titleLabel").objectReferenceValue = title;
            cardObject.FindProperty("descLabel").objectReferenceValue = desc;
            cardObject.FindProperty("picture").objectReferenceValue = picture;
            cardObject.FindProperty("background").objectReferenceValue = background;
            cardObject.FindProperty("claimButton").objectReferenceValue = claim;
            cardObject.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        // Anchored to the card's top-left corner; position is the label's own top-left corner.
        private static TextMeshProUGUI CreateLabel(string objectName, string text, RectTransform parent, float fontSize,
            FontStyles style, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect(objectName, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(string objectName, string caption, RectTransform parent, Sprite uiSprite, float fontSize)
        {
            RectTransform rect = CreateRect(objectName, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = uiSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            RectTransform labelRect = CreateRect("Label", rect);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = caption;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return button;
        }

        private static RectTransform CreateRect(string objectName, RectTransform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = UILayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        // First CardCount sprites in SpriteFolder, natural name order. Empty list if the folder is missing.
        private static List<Sprite> CollectSprites()
        {
            var sprites = new List<Sprite>();
            if (!AssetDatabase.IsValidFolder(SpriteFolder))
                return sprites;

            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { SpriteFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!paths.Contains(path))
                    paths.Add(path);
            }
            paths.Sort(EditorUtility.NaturalCompare);

            foreach (string path in paths)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                    continue;
                sprites.Add(sprite);
                if (sprites.Count >= CardCount)
                    break;
            }
            return sprites;
        }
    }
}
