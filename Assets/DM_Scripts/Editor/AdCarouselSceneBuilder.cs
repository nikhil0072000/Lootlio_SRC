using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DMScripts.Editor
{
	// Tools > Carousel > Create Ad Carousel Scene
	// Builds Assets/DM_Scenes/AdCarousel.unity: a Canvas with a UICarousel stretched across the bottom
	// edge of the screen, showing the first 5 sprites found in Assets/Em_Sprites.
	public static class AdCarouselSceneBuilder
	{
		private const string SceneFolder = "Assets/DM_Scenes";

		private const string ScenePath = SceneFolder + "/AdCarousel.unity";

		private const string SpriteFolder = "Assets/Em_Sprites";

		private const int PageCount = 5;

		private const int UILayer = 5;

		// Same reference resolution as the main gameplay canvas.
		private static readonly Vector2 ReferenceResolution = new Vector2(640f, 960f);

		private const float CarouselHeight = 260f;

		private const float DotsHeight = 24f;

		private const float ButtonSize = 48f;

		[MenuItem("Tools/Carousel/Create Ad Carousel Scene")]
		public static void CreateScene()
		{
			List<Sprite> sprites = CollectSprites();
			if (sprites.Count < PageCount)
			{
				EditorUtility.DisplayDialog("Ad Carousel", "Need " + PageCount + " sprites in " + SpriteFolder + " but found " + sprites.Count + ". Set the images' Texture Type to 'Sprite (2D and UI)'.", "OK");
				return;
			}
			if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Ad Carousel", ScenePath + " already exists. Overwrite it?", "Overwrite", "Cancel"))
			{
				return;
			}
			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			CreateCamera();
			RectTransform canvas = CreateCanvas();
			CreateEventSystem();
			GameObject carousel = BuildCarousel(canvas, sprites);

			if (!AssetDatabase.IsValidFolder(SceneFolder))
			{
				AssetDatabase.CreateFolder("Assets", "DM_Scenes");
			}
			if (!EditorSceneManager.SaveScene(scene, ScenePath))
			{
				Debug.LogError("Ad Carousel: could not save the scene to " + ScenePath + ".");
				return;
			}
			Selection.activeGameObject = carousel;
			Debug.Log("Ad carousel scene created at " + ScenePath + " with " + sprites.Count + " pages.");
		}

		private static void CreateCamera()
		{
			GameObject gameObject = new GameObject("Main Camera");
			gameObject.tag = "MainCamera";
			gameObject.transform.position = new Vector3(0f, 0f, -10f);
			Camera camera = gameObject.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = new Color(0.1f, 0.16f, 0.24f, 1f);
			camera.orthographic = true;
		}

		private static RectTransform CreateCanvas()
		{
			GameObject gameObject = new GameObject("Canvas", typeof(RectTransform));
			gameObject.layer = UILayer;
			Canvas canvas = gameObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			CanvasScaler canvasScaler = gameObject.AddComponent<CanvasScaler>();
			canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			canvasScaler.referenceResolution = ReferenceResolution;
			canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
			canvasScaler.matchWidthOrHeight = 1f;
			gameObject.AddComponent<GraphicRaycaster>();
			return (RectTransform)gameObject.transform;
		}

		private static void CreateEventSystem()
		{
			// The project uses the legacy Input Manager, so the standalone module is the right one.
			GameObject gameObject = new GameObject("EventSystem");
			gameObject.AddComponent<EventSystem>();
			gameObject.AddComponent<StandaloneInputModule>();
		}

		private static GameObject BuildCarousel(RectTransform canvas, List<Sprite> sprites)
		{
			Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
			Sprite knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
			Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

			// Stretched across the bottom edge of the screen.
			RectTransform root = CreateRect("AdCarousel", canvas);
			root.anchorMin = new Vector2(0f, 0f);
			root.anchorMax = new Vector2(1f, 0f);
			root.pivot = new Vector2(0.5f, 0f);
			root.sizeDelta = new Vector2(0f, CarouselHeight);
			root.anchoredPosition = Vector2.zero;
			ScrollRect scrollRect = root.gameObject.AddComponent<ScrollRect>();
			UICarousel carousel = root.gameObject.AddComponent<UICarousel>();

			RectTransform viewport = CreateRect("Viewport", root);
			Stretch(viewport, new Vector2(0f, DotsHeight + 6f), Vector2.zero);
			viewport.gameObject.AddComponent<RectMask2D>();
			// Transparent image so drags anywhere in the viewport reach the ScrollRect.
			Image viewportImage = viewport.gameObject.AddComponent<Image>();
			viewportImage.color = new Color(0f, 0f, 0f, 0f);

			RectTransform content = CreateRect("Content", viewport);
			content.anchorMin = new Vector2(0f, 0f);
			content.anchorMax = new Vector2(0f, 1f);
			content.pivot = new Vector2(0f, 0.5f);
			content.anchoredPosition = Vector2.zero;

			// UICarousel instantiates this once per sprite at Start.
			RectTransform template = CreateRect("PageTemplate", content);
			Image templateImage = template.gameObject.AddComponent<Image>();
			templateImage.preserveAspect = true;
			template.gameObject.SetActive(value: false);

			RectTransform dotsRoot = CreateRect("Dots", root);
			dotsRoot.anchorMin = new Vector2(0f, 0f);
			dotsRoot.anchorMax = new Vector2(1f, 0f);
			dotsRoot.pivot = new Vector2(0.5f, 0f);
			dotsRoot.sizeDelta = new Vector2(0f, DotsHeight);
			dotsRoot.anchoredPosition = new Vector2(0f, 4f);
			HorizontalLayoutGroup dotsLayout = dotsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
			dotsLayout.childAlignment = TextAnchor.MiddleCenter;
			dotsLayout.spacing = 10f;
			dotsLayout.childControlWidth = true;
			dotsLayout.childControlHeight = true;
			dotsLayout.childForceExpandWidth = false;
			dotsLayout.childForceExpandHeight = false;

			float buttonY = (DotsHeight + 6f) * 0.5f;
			Button prev = CreateArrowButton("ButtonPrev", "<", root, new Vector2(0f, 0.5f), new Vector2(ButtonSize * 0.6f, buttonY), uiSprite, font);
			Button next = CreateArrowButton("ButtonNext", ">", root, new Vector2(1f, 0.5f), new Vector2((0f - ButtonSize) * 0.6f, buttonY), uiSprite, font);

			scrollRect.viewport = viewport;
			scrollRect.content = content;
			scrollRect.horizontal = true;
			scrollRect.vertical = false;
			scrollRect.inertia = false;
			scrollRect.movementType = ScrollRect.MovementType.Unrestricted;

			carousel.scrollRect = scrollRect;
			carousel.content = content;
			carousel.pageTemplate = template;
			carousel.dotsRoot = dotsRoot;
			carousel.buttonPrev = prev;
			carousel.buttonNext = next;
			carousel.sprites = sprites;
			carousel.fitToViewport = true;
			carousel.viewportFill = 0.8f;
			carousel.preserveAspect = true;
			carousel.loop = true;
			carousel.dotSprite = knobSprite;
			carousel.dotSize = new Vector2(14f, 14f);
			return root.gameObject;
		}

		// First PageCount distinct sprites in SpriteFolder (top-level images only, natural name order).
		// Textures that are not imported as sprites are skipped.
		private static List<Sprite> CollectSprites()
		{
			List<Sprite> list = new List<Sprite>();
			if (!AssetDatabase.IsValidFolder(SpriteFolder))
			{
				return list;
			}
			List<string> paths = new List<string>();
			foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new string[1] { SpriteFolder }))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (Path.GetDirectoryName(path).Replace('\\', '/') == SpriteFolder && !paths.Contains(path))
				{
					paths.Add(path);
				}
			}
			paths.Sort(EditorUtility.NaturalCompare);
			foreach (string path in paths)
			{
				if (list.Count >= PageCount)
				{
					break;
				}
				Sprite sprite = FirstSpriteAtPath(path);
				if (sprite != null && !list.Contains(sprite))
				{
					list.Add(sprite);
				}
			}
			return list;
		}

		// Works for Single and Multiple sprite modes.
		private static Sprite FirstSpriteAtPath(string path)
		{
			foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
			{
				if (asset is Sprite sprite)
				{
					return sprite;
				}
			}
			return null;
		}

		private static Button CreateArrowButton(string name, string label, RectTransform parent, Vector2 anchor, Vector2 position, Sprite background, Font font)
		{
			RectTransform rect = CreateRect(name, parent);
			rect.anchorMin = anchor;
			rect.anchorMax = anchor;
			rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
			rect.anchoredPosition = position;
			Image image = rect.gameObject.AddComponent<Image>();
			image.sprite = background;
			image.type = Image.Type.Sliced;
			image.color = new Color(0f, 0f, 0f, 0.45f);
			Button button = rect.gameObject.AddComponent<Button>();
			RectTransform textRect = CreateRect("Text", rect);
			Stretch(textRect, Vector2.zero, Vector2.zero);
			Text text = textRect.gameObject.AddComponent<Text>();
			text.text = label;
			text.font = font;
			text.fontSize = Mathf.RoundToInt(ButtonSize * 0.6f);
			text.fontStyle = FontStyle.Bold;
			text.alignment = TextAnchor.MiddleCenter;
			text.color = Color.white;
			text.raycastTarget = false;
			return button;
		}

		private static RectTransform CreateRect(string name, Transform parent)
		{
			GameObject gameObject = new GameObject(name, typeof(RectTransform));
			gameObject.layer = UILayer;
			RectTransform rectTransform = (RectTransform)gameObject.transform;
			if (parent != null)
			{
				rectTransform.SetParent(parent, worldPositionStays: false);
			}
			return rectTransform;
		}

		private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.offsetMin = offsetMin;
			rect.offsetMax = offsetMax;
		}
	}
}
