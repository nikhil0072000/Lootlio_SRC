using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Tools > Carousel > Create Carousel Prefab
// Builds Assets/Prefab/UI/UICarousel.prefab. Page images come from the sprites/textures selected
// in the Project window, or from Assets/Em_Sprites when nothing is selected.
public static class UICarouselPrefabBuilder
{
	private const string PrefabFolder = "Assets/Prefab/UI";

	private const string PrefabPath = PrefabFolder + "/UICarousel.prefab";

	private const string FallbackSpriteFolder = "Assets/Em_Sprites";

	private static readonly Vector2 RootSize = new Vector2(1000f, 560f);

	private static readonly Vector2 PageSize = new Vector2(600f, 400f);

	private const float Spacing = 40f;

	private const int UILayer = 5;

	[MenuItem("Tools/Carousel/Create Carousel Prefab")]
	public static void CreatePrefab()
	{
		List<Sprite> sprites = CollectSprites();
		if (sprites.Count == 0)
		{
			EditorUtility.DisplayDialog("Carousel", "No sprites found. Select some sprites in the Project window, or put images in " + FallbackSpriteFolder + ".", "OK");
			return;
		}
		if (File.Exists(PrefabPath) && !EditorUtility.DisplayDialog("Carousel", PrefabPath + " already exists. Overwrite it?", "Overwrite", "Cancel"))
		{
			return;
		}
		GameObject root = Build(sprites);
		try
		{
			if (!AssetDatabase.IsValidFolder(PrefabFolder))
			{
				AssetDatabase.CreateFolder("Assets/Prefab", "UI");
			}
			GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
			Selection.activeObject = prefab;
			EditorGUIUtility.PingObject(prefab);
			Debug.Log("UICarousel prefab created at " + PrefabPath + " with " + sprites.Count + " pages.", prefab);
		}
		finally
		{
			Object.DestroyImmediate(root);
		}
	}

	[MenuItem("Tools/Carousel/Make Selected Scroll View A Carousel")]
	public static void SetupSelected()
	{
		ScrollRect scrollRect = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<ScrollRect>() : null;
		if (scrollRect == null)
		{
			EditorUtility.DisplayDialog("Carousel", "Select a GameObject that has a Scroll Rect (a UI Scroll View) in the Hierarchy first.", "OK");
			return;
		}
		SetupScrollView(scrollRect);
	}

	[MenuItem("CONTEXT/ScrollRect/Make Carousel (swipe + arrows + dots)")]
	private static void SetupFromContext(MenuCommand command)
	{
		SetupScrollView((ScrollRect)command.context);
	}

	// Turns an existing Scroll View into a UICarousel: removes scrollbars and layout components that would
	// fight the carousel's own layout, then adds arrow buttons and a dots row on the same layer as the Scroll View.
	public static void SetupScrollView(ScrollRect scrollRect)
	{
		GameObject go = scrollRect.gameObject;
		if (scrollRect.content == null)
		{
			EditorUtility.DisplayDialog("Carousel", "The Scroll Rect has no Content assigned.", "OK");
			return;
		}
		Undo.RegisterFullObjectHierarchyUndo(go, "Make Carousel");
		int layer = go.layer;
		RectTransform root = (RectTransform)go.transform;

		// Scrollbars
		foreach (Scrollbar scrollbar in new Scrollbar[2] { scrollRect.horizontalScrollbar, scrollRect.verticalScrollbar })
		{
			if ((bool)scrollbar)
			{
				scrollbar.gameObject.SetActive(value: false);
			}
		}
		scrollRect.horizontalScrollbar = null;
		scrollRect.verticalScrollbar = null;
		scrollRect.horizontal = true;
		scrollRect.vertical = false;
		scrollRect.inertia = false;
		scrollRect.movementType = ScrollRect.MovementType.Unrestricted;

		// Viewport fills the whole Scroll View now that the scrollbars are gone.
		RectTransform viewport = scrollRect.viewport;
		if (viewport != null && viewport != root)
		{
			Stretch(viewport, Vector2.zero, Vector2.zero);
		}

		// The carousel positions pages itself, so layout groups / fitters on Content must be off.
		foreach (Behaviour behaviour in scrollRect.content.GetComponents<Behaviour>())
		{
			if (behaviour is LayoutGroup || behaviour is ContentSizeFitter)
			{
				behaviour.enabled = false;
			}
		}

		Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
		Sprite knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
		Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		float buttonSize = Mathf.Clamp(root.rect.height * 0.25f, 40f, 80f);

		Button prev = FindOrCreateButton(root, "ButtonPrev", "<", new Vector2(0f, 0.5f), new Vector2(buttonSize * 0.6f, 0f), buttonSize, uiSprite, font, layer);
		Button next = FindOrCreateButton(root, "ButtonNext", ">", new Vector2(1f, 0.5f), new Vector2((0f - buttonSize) * 0.6f, 0f), buttonSize, uiSprite, font, layer);

		RectTransform dotsRoot = root.Find("Dots") as RectTransform;
		if (dotsRoot == null)
		{
			dotsRoot = CreateRect("Dots", root);
			Undo.RegisterCreatedObjectUndo(dotsRoot.gameObject, "Make Carousel");
			dotsRoot.anchorMin = new Vector2(0f, 0f);
			dotsRoot.anchorMax = new Vector2(1f, 0f);
			dotsRoot.pivot = new Vector2(0.5f, 0f);
			dotsRoot.sizeDelta = new Vector2(0f, 24f);
			dotsRoot.anchoredPosition = new Vector2(0f, 4f);
			HorizontalLayoutGroup dotsLayout = dotsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
			dotsLayout.childAlignment = TextAnchor.MiddleCenter;
			dotsLayout.spacing = 10f;
			dotsLayout.childControlWidth = true;
			dotsLayout.childControlHeight = true;
			dotsLayout.childForceExpandWidth = false;
			dotsLayout.childForceExpandHeight = false;
		}
		dotsRoot.gameObject.layer = layer;
		dotsRoot.SetAsLastSibling();

		UICarousel carousel = go.GetComponent<UICarousel>();
		if (carousel == null)
		{
			carousel = Undo.AddComponent<UICarousel>(go);
			carousel.fitToViewport = true;
			carousel.viewportFill = 0.8f;
			carousel.dotSize = new Vector2(14f, 14f);
			carousel.dotSprite = knobSprite;
		}
		carousel.scrollRect = scrollRect;
		carousel.content = scrollRect.content;
		carousel.dotsRoot = dotsRoot;
		carousel.buttonPrev = prev;
		carousel.buttonNext = next;
		EditorUtility.SetDirty(carousel);
		UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);

		int pageCount = 0;
		foreach (Transform child in scrollRect.content)
		{
			if (child.gameObject.activeSelf)
			{
				pageCount++;
			}
		}
		Debug.Log("Carousel set up on '" + go.name + "' with " + pageCount + " page(s). Each active child of '" + scrollRect.content.name + "' is one page; duplicate one and change its sprite to add more.", go);
		Selection.activeGameObject = go;
	}

	private static Button FindOrCreateButton(RectTransform root, string name, string label, Vector2 anchor, Vector2 position, float size, Sprite background, Font font, int layer)
	{
		Transform existing = root.Find(name);
		if (existing != null && existing.GetComponent<Button>() != null)
		{
			return existing.GetComponent<Button>();
		}
		Button button = CreateArrowButton(name, label, root, anchor, position, background, font);
		RectTransform rect = (RectTransform)button.transform;
		rect.sizeDelta = new Vector2(size, size);
		rect.GetComponentInChildren<Text>().fontSize = Mathf.RoundToInt(size * 0.6f);
		foreach (Transform transform in rect.GetComponentsInChildren<Transform>(includeInactive: true))
		{
			transform.gameObject.layer = layer;
		}
		Undo.RegisterCreatedObjectUndo(button.gameObject, "Make Carousel");
		return button;
	}

	private static List<Sprite> CollectSprites()
	{
		List<Sprite> list = new List<Sprite>();
		foreach (Object obj in Selection.objects)
		{
			AddSpritesAtPath(AssetDatabase.GetAssetPath(obj), obj as Sprite, list);
		}
		if (list.Count == 0 && AssetDatabase.IsValidFolder(FallbackSpriteFolder))
		{
			// Top-level images only, sorted by name so img1, img2, img3... keep their order.
			IEnumerable<string> paths = AssetDatabase.FindAssets("t:Texture2D", new string[1] { FallbackSpriteFolder })
				.Select(AssetDatabase.GUIDToAssetPath)
				.Where((string p) => Path.GetDirectoryName(p).Replace('\\', '/') == FallbackSpriteFolder)
				.OrderBy((string p) => p, Comparer<string>.Create(EditorUtility.NaturalCompare));
			foreach (string path in paths)
			{
				AddSpritesAtPath(path, null, list);
			}
		}
		return list;
	}

	private static void AddSpritesAtPath(string path, Sprite exact, List<Sprite> list)
	{
		if (string.IsNullOrEmpty(path))
		{
			return;
		}
		if (exact != null)
		{
			if (!list.Contains(exact))
			{
				list.Add(exact);
			}
			return;
		}
		// Texture selected: take its first sprite (works for Single and Multiple sprite modes).
		Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
		if (sprite == null)
		{
			Debug.LogWarning("Carousel: " + path + " has no sprite. Set its Texture Type to 'Sprite (2D and UI)'.");
		}
		else if (!list.Contains(sprite))
		{
			list.Add(sprite);
		}
	}

	private static GameObject Build(List<Sprite> sprites)
	{
		Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
		Sprite knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
		Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

		RectTransform root = CreateRect("UICarousel", null);
		root.sizeDelta = RootSize;
		ScrollRect scrollRect = root.gameObject.AddComponent<ScrollRect>();
		UICarousel carousel = root.gameObject.AddComponent<UICarousel>();

		RectTransform viewport = CreateRect("Viewport", root);
		Stretch(viewport, new Vector2(0f, 60f), Vector2.zero);
		viewport.gameObject.AddComponent<RectMask2D>();
		// Transparent image so drags anywhere in the viewport reach the ScrollRect.
		Image viewportImage = viewport.gameObject.AddComponent<Image>();
		viewportImage.color = new Color(0f, 0f, 0f, 0f);

		RectTransform content = CreateRect("Content", viewport);
		content.anchorMin = new Vector2(0f, 0f);
		content.anchorMax = new Vector2(0f, 1f);
		content.pivot = new Vector2(0f, 0.5f);
		content.anchoredPosition = Vector2.zero;

		RectTransform template = CreateRect("PageTemplate", content);
		Image templateImage = template.gameObject.AddComponent<Image>();
		templateImage.preserveAspect = true;
		template.gameObject.SetActive(value: false);

		// Lay pages out the same way UICarousel does at runtime so the prefab looks right in edit mode.
		float viewportWidth = RootSize.x;
		float padding = Mathf.Max(0f, (viewportWidth - PageSize.x) * 0.5f);
		content.sizeDelta = new Vector2(padding * 2f + sprites.Count * PageSize.x + (sprites.Count - 1) * Spacing, 0f);
		for (int i = 0; i < sprites.Count; i++)
		{
			RectTransform page = CreateRect("Page_" + (i + 1), content);
			page.anchorMin = new Vector2(0f, 0.5f);
			page.anchorMax = new Vector2(0f, 0.5f);
			page.sizeDelta = PageSize;
			page.anchoredPosition = new Vector2(padding + i * (PageSize.x + Spacing) + PageSize.x * 0.5f, 0f);
			Image image = page.gameObject.AddComponent<Image>();
			image.sprite = sprites[i];
			image.preserveAspect = true;
			page.gameObject.AddComponent<CanvasGroup>();
			if (i > 0)
			{
				page.localScale = new Vector3(0.8f, 0.8f, 1f);
				page.GetComponent<CanvasGroup>().alpha = 0.6f;
			}
		}

		RectTransform dotsRoot = CreateRect("Dots", root);
		dotsRoot.anchorMin = new Vector2(0f, 0f);
		dotsRoot.anchorMax = new Vector2(1f, 0f);
		dotsRoot.pivot = new Vector2(0.5f, 0f);
		dotsRoot.sizeDelta = new Vector2(0f, 50f);
		dotsRoot.anchoredPosition = new Vector2(0f, 5f);
		HorizontalLayoutGroup dotsLayout = dotsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
		dotsLayout.childAlignment = TextAnchor.MiddleCenter;
		dotsLayout.spacing = 16f;
		dotsLayout.childControlWidth = true;
		dotsLayout.childControlHeight = true;
		dotsLayout.childForceExpandWidth = false;
		dotsLayout.childForceExpandHeight = false;

		Button prev = CreateArrowButton("ButtonPrev", "<", root, new Vector2(0f, 0.5f), new Vector2(50f, 30f), uiSprite, font);
		Button next = CreateArrowButton("ButtonNext", ">", root, new Vector2(1f, 0.5f), new Vector2(-50f, 30f), uiSprite, font);

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
		carousel.pageSize = PageSize;
		carousel.spacing = Spacing;
		carousel.dotSprite = knobSprite;
		return root.gameObject;
	}

	private static Button CreateArrowButton(string name, string label, RectTransform parent, Vector2 anchor, Vector2 position, Sprite background, Font font)
	{
		RectTransform rect = CreateRect(name, parent);
		rect.anchorMin = anchor;
		rect.anchorMax = anchor;
		rect.sizeDelta = new Vector2(80f, 80f);
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
		text.fontSize = 48;
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
