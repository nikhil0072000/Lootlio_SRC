using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Horizontal carousel built on a ScrollRect: snaps one page at a time,
// scales/fades side pages, and drives page dots and prev/next buttons.
// Pages are laid out by this script (no layout group) so the centering math stays exact.
[RequireComponent(typeof(ScrollRect))]
public class UICarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	[Header("References")]
	public ScrollRect scrollRect;

	public RectTransform content;

	// Optional: instantiated for each sprite in 'sprites'. Must have an Image on its root.
	public RectTransform pageTemplate;

	public RectTransform dotsRoot;

	public Button buttonPrev;

	public Button buttonNext;

	[Header("Pages")]
	// When not empty, pages are (re)built from these sprites at Start. When empty, existing children of 'content' are used.
	public List<Sprite> sprites = new List<Sprite>();

	public Vector2 pageSize = new Vector2(600f, 400f);

	// When on, pageSize is recalculated from the viewport so the carousel fits any size.
	public bool fitToViewport;

	// Fraction of the viewport a page fills; below 1 lets the neighbouring pages peek in at the sides.
	[Range(0.3f, 1f)]
	public float viewportFill = 0.8f;

	public float spacing = 40f;

	public bool preserveAspect = true;

	[Header("Carousel Look")]
	[Range(0.3f, 1f)]
	public float sideScale = 0.8f;

	[Range(0f, 1f)]
	public float sideAlpha = 0.6f;

	[Header("Snapping")]
	public float snapSpeed = 12f;

	// Drag distance (in canvas units) that flips to the next page even if the page centre is not crossed.
	public float swipeThreshold = 60f;

	public bool loop;

	[Header("Dots")]
	public Sprite dotSprite;

	public Vector2 dotSize = new Vector2(20f, 20f);

	public Color dotActiveColor = Color.white;

	public Color dotInactiveColor = new Color(1f, 1f, 1f, 0.35f);

	public event Action<int> OnPageChanged;

	private readonly List<RectTransform> pages = new List<RectTransform>();

	private readonly List<CanvasGroup> pageGroups = new List<CanvasGroup>();

	private readonly List<Image> dots = new List<Image>();

	private int currentPage;

	private bool isSnapping;

	private float dragStartX;

	private float lastViewportWidth = -1f;

	public int CurrentPage => currentPage;

	public int PageCount => pages.Count;

	private RectTransform Viewport => scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;

	private float Step => pageSize.x + spacing;

	private void Awake()
	{
		if (scrollRect == null)
		{
			scrollRect = GetComponent<ScrollRect>();
		}
		if (content == null)
		{
			content = scrollRect.content;
		}
		scrollRect.horizontal = true;
		scrollRect.vertical = false;
		scrollRect.inertia = false;
		scrollRect.movementType = ScrollRect.MovementType.Unrestricted;
		// Each canvas in this project is drawn by a camera that only renders the canvas's own layer.
		Canvas canvas = GetComponentInParent<Canvas>();
		if ((bool)canvas)
		{
			SetLayerRecursively(base.transform, canvas.gameObject.layer);
		}
		if ((bool)buttonPrev)
		{
			buttonPrev.onClick.AddListener(Prev);
		}
		if ((bool)buttonNext)
		{
			buttonNext.onClick.AddListener(Next);
		}
	}

	private static void SetLayerRecursively(Transform target, int layer)
	{
		target.gameObject.layer = layer;
		foreach (Transform child in target)
		{
			SetLayerRecursively(child, layer);
		}
	}

	private void Start()
	{
		if (sprites.Count > 0)
		{
			SetSprites(sprites);
		}
		else
		{
			CollectExistingPages();
		}
	}

	// Replace all pages with the given sprites at runtime.
	public void SetSprites(IList<Sprite> newSprites)
	{
		if (pageTemplate == null)
		{
			Debug.LogWarning("UICarousel: pageTemplate is not assigned, cannot build pages from sprites.", this);
			return;
		}
		pageTemplate.gameObject.SetActive(value: false);
		for (int num = content.childCount - 1; num >= 0; num--)
		{
			Transform child = content.GetChild(num);
			if (child != pageTemplate)
			{
				child.gameObject.SetActive(value: false);
				Destroy(child.gameObject);
			}
		}
		pages.Clear();
		for (int i = 0; i < newSprites.Count; i++)
		{
			RectTransform page = Instantiate(pageTemplate, content);
			page.name = "Page_" + (i + 1);
			page.gameObject.SetActive(value: true);
			Image image = page.GetComponent<Image>();
			if ((bool)image)
			{
				image.sprite = newSprites[i];
				image.preserveAspect = preserveAspect;
			}
			pages.Add(page);
		}
		Rebuild();
	}

	private void CollectExistingPages()
	{
		pages.Clear();
		foreach (RectTransform child in content)
		{
			if (child != pageTemplate && child.gameObject.activeSelf)
			{
				pages.Add(child);
				Image image = child.GetComponent<Image>();
				if ((bool)image)
				{
					image.preserveAspect = preserveAspect;
				}
			}
		}
		Rebuild();
	}

	private void Rebuild()
	{
		pageGroups.Clear();
		foreach (RectTransform page in pages)
		{
			CanvasGroup canvasGroup = page.GetComponent<CanvasGroup>();
			if (canvasGroup == null)
			{
				canvasGroup = page.gameObject.AddComponent<CanvasGroup>();
			}
			pageGroups.Add(canvasGroup);
		}
		BuildDots();
		LayoutPages();
		currentPage = Mathf.Clamp(currentPage, 0, Mathf.Max(0, pages.Count - 1));
		JumpTo(currentPage);
	}

	private void LayoutPages()
	{
		float viewportWidth = Viewport.rect.width;
		lastViewportWidth = viewportWidth;
		if (fitToViewport)
		{
			pageSize = new Vector2(viewportWidth * viewportFill, Viewport.rect.height * viewportFill);
		}
		// Side padding lets the first and last page sit in the centre of the viewport.
		float padding = Mathf.Max(0f, (viewportWidth - pageSize.x) * 0.5f);
		content.anchorMin = new Vector2(0f, 0f);
		content.anchorMax = new Vector2(0f, 1f);
		content.pivot = new Vector2(0f, 0.5f);
		float width = padding * 2f + pages.Count * pageSize.x + Mathf.Max(0, pages.Count - 1) * spacing;
		content.sizeDelta = new Vector2(width, 0f);
		for (int i = 0; i < pages.Count; i++)
		{
			RectTransform page = pages[i];
			page.anchorMin = new Vector2(0f, 0.5f);
			page.anchorMax = new Vector2(0f, 0.5f);
			page.pivot = new Vector2(0.5f, 0.5f);
			page.sizeDelta = pageSize;
			page.anchoredPosition = new Vector2(padding + i * Step + pageSize.x * 0.5f, 0f);
		}
	}

	private void BuildDots()
	{
		if (dotsRoot == null)
		{
			return;
		}
		for (int num = dotsRoot.childCount - 1; num >= 0; num--)
		{
			GameObject obj = dotsRoot.GetChild(num).gameObject;
			obj.SetActive(value: false);
			Destroy(obj);
		}
		dots.Clear();
		for (int i = 0; i < pages.Count; i++)
		{
			GameObject gameObject = new GameObject("Dot_" + (i + 1), typeof(RectTransform), typeof(Image), typeof(Button));
			gameObject.layer = dotsRoot.gameObject.layer;
			RectTransform rectTransform = (RectTransform)gameObject.transform;
			rectTransform.SetParent(dotsRoot, worldPositionStays: false);
			rectTransform.sizeDelta = dotSize;
			LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
			layoutElement.preferredWidth = dotSize.x;
			layoutElement.preferredHeight = dotSize.y;
			Image image = gameObject.GetComponent<Image>();
			image.sprite = dotSprite;
			int index = i;
			gameObject.GetComponent<Button>().onClick.AddListener(delegate
			{
				GoTo(index);
			});
			dots.Add(image);
		}
	}

	private float TargetX(int page)
	{
		return (0f - page) * Step;
	}

	private int NearestPage()
	{
		if (pages.Count == 0)
		{
			return 0;
		}
		return Mathf.Clamp(Mathf.RoundToInt((0f - content.anchoredPosition.x) / Step), 0, pages.Count - 1);
	}

	public void Next()
	{
		if (pages.Count != 0)
		{
			int num = currentPage + 1;
			GoTo((num < pages.Count) ? num : (loop ? 0 : currentPage));
		}
	}

	public void Prev()
	{
		if (pages.Count != 0)
		{
			int num = currentPage - 1;
			GoTo((num >= 0) ? num : (loop ? (pages.Count - 1) : currentPage));
		}
	}

	public void GoTo(int page)
	{
		if (pages.Count != 0)
		{
			SetCurrentPage(Mathf.Clamp(page, 0, pages.Count - 1));
			isSnapping = true;
		}
	}

	public void JumpTo(int page)
	{
		if (pages.Count != 0)
		{
			SetCurrentPage(Mathf.Clamp(page, 0, pages.Count - 1));
			content.anchoredPosition = new Vector2(TargetX(currentPage), content.anchoredPosition.y);
			isSnapping = false;
			UpdateVisuals();
		}
	}

	private void SetCurrentPage(int page)
	{
		bool changed = page != currentPage;
		currentPage = page;
		for (int i = 0; i < dots.Count; i++)
		{
			dots[i].color = (i == currentPage) ? dotActiveColor : dotInactiveColor;
		}
		if ((bool)buttonPrev)
		{
			buttonPrev.interactable = loop || currentPage > 0;
		}
		if ((bool)buttonNext)
		{
			buttonNext.interactable = loop || currentPage < pages.Count - 1;
		}
		if (changed)
		{
			OnPageChanged?.Invoke(currentPage);
		}
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		isSnapping = false;
		dragStartX = content.anchoredPosition.x;
	}

	public void OnDrag(PointerEventData eventData)
	{
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		scrollRect.velocity = Vector2.zero;
		float num = content.anchoredPosition.x - dragStartX;
		int num2 = NearestPage();
		if (num2 == currentPage && Mathf.Abs(num) > swipeThreshold)
		{
			num2 += (num < 0f) ? 1 : (-1);
		}
		GoTo(num2);
	}

	private void Update()
	{
		if (pages.Count == 0)
		{
			return;
		}
		if (!Mathf.Approximately(Viewport.rect.width, lastViewportWidth))
		{
			LayoutPages();
			JumpTo(currentPage);
		}
		if (isSnapping)
		{
			Vector2 anchoredPosition = content.anchoredPosition;
			float num = TargetX(currentPage);
			anchoredPosition.x = Mathf.Lerp(anchoredPosition.x, num, 1f - Mathf.Exp((0f - snapSpeed) * Time.unscaledDeltaTime));
			if (Mathf.Abs(anchoredPosition.x - num) < 0.5f)
			{
				anchoredPosition.x = num;
				isSnapping = false;
			}
			content.anchoredPosition = anchoredPosition;
		}
		UpdateVisuals();
	}

	// Scale and fade each page by its distance (in pages) from the viewport centre.
	private void UpdateVisuals()
	{
		float num = 0f - content.anchoredPosition.x;
		for (int i = 0; i < pages.Count; i++)
		{
			float t = Mathf.Clamp01(Mathf.Abs(i * Step - num) / Step);
			float num2 = Mathf.Lerp(1f, sideScale, t);
			pages[i].localScale = new Vector3(num2, num2, 1f);
			pageGroups[i].alpha = Mathf.Lerp(1f, sideAlpha, t);
		}
	}
}
