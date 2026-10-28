using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GeoAds
{
    /// <summary>
    /// Bottom, full-width ad carousel: one card fills the viewport, swiping moves exactly one
    /// card and release snaps to that page.
    ///
    /// Three ways to use it:
    /// - template (the normal path): CardTemplate is a card that already exists under Content in
    ///   the prefab. Populate() clones it once per offer, so editing that one card in the editor
    ///   edits every card. Nothing is positioned by code - all rects come from the prefab.
    /// - hand-made: Cards is filled in the Inspector with cards that already exist. Offers fill
    ///   those cards, spare cards are hidden.
    /// - code-built (CardTemplate and Cards both empty): builds its own canvas, viewport and
    ///   cards from code - no prefab or scene wiring required.
    /// </summary>
    public class GeoAdCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Cards")]
        [Tooltip("The one card to clone for every offer. Leave empty to use Hand-made Cards.")]
        [SerializeField] private GeoAdCard cardTemplate;
        [Tooltip("Legacy hand-made cards, used only when Card Template is empty.")]
        [SerializeField] private GeoAdCard[] cards;

        [Header("Wiring (assigned by Tools > GeoAds > Build Neon Carousel)")]
        [SerializeField] private RectTransform content;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private Button closeButton; // optional

        [Header("Paging")]
        [Tooltip("Seconds to glide to the next card after your finger lifts. 0 = jump.")]
        [SerializeField] private float snapSeconds = 0.25f;

        private const int SortOrder = 900;   // above popups, below debugging overlays

        private Canvas canvas;
        private bool closeHooked;
        private readonly System.Collections.Generic.List<GameObject> clones = new System.Collections.Generic.List<GameObject>();
        private int pageCount;

        private bool HasTemplate => cardTemplate != null;
        private bool IsHandMade => cards != null && cards.Length > 0;

        /// <summary>
        /// True between the start of a swipe and the frame after it ends, so a tap that was
        /// really a page-change never triggers a card's button.
        /// </summary>
        public static bool InputLocked { get; private set; }

        public void Show()
        {
            if (!HasTemplate && !IsHandMade)
                EnsureBuilt();
            else
                ResolveContent();

            HookCloseButton();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (this != null && gameObject != null)
                gameObject.SetActive(false);
        }

        public void Populate(GeoAdOffer[] offers)
        {
            if (HasTemplate)
            {
                PopulateFromTemplate(offers);
                return;
            }
            if (IsHandMade)
            {
                PopulateHandMade(offers);
                return;
            }
            PopulateCodeBuilt(offers);
        }

        /// <summary>Clones CardTemplate once per offer, so all 18 cards are identical by construction.</summary>
        private void PopulateFromTemplate(GeoAdOffer[] offers)
        {
            ResolveContent();

            for (int i = clones.Count - 1; i >= 0; i--)
                if (clones[i] != null)
                    Destroy(clones[i]);
            clones.Clear();

            int count = offers != null ? offers.Length : 0;
            if (count == 0)
            {
                cardTemplate.gameObject.SetActive(false);
                pageCount = 0;
                return;
            }

            cardTemplate.gameObject.SetActive(true);

            // clone from the clean prefab look, never from a card holding a downloaded photo
            cardTemplate.ResetImages();

            for (int i = 1; i < count; i++)
            {
                GameObject clone = Instantiate(cardTemplate.gameObject, content);
                clone.name = "GeoAdCard_" + (i + 1);
                clone.SetActive(true);
                GeoAdCard card = clone.GetComponent<GeoAdCard>();
                if (card != null)
                    card.Bind(offers[i]);
                clones.Add(clone);
            }

            cardTemplate.Bind(offers[0]); // last, after cloning; it stays the first sibling, so order is 1..N

            pageCount = count;
            RebuildLayout();
        }

        /// <summary>Offer 1..N goes to card 1..N in list order; spare cards are hidden.</summary>
        private void PopulateHandMade(GeoAdOffer[] offers)
        {
            HookCloseButton();

            int offerCount = offers != null ? offers.Length : 0;
            for (int i = 0; i < cards.Length; i++)
            {
                GeoAdCard card = cards[i];
                if (card == null)
                    continue;

                bool used = i < offerCount && offers[i] != null;
                card.gameObject.SetActive(used);
                if (used)
                    card.Bind(offers[i]);
            }

            pageCount = Mathf.Min(offerCount, cards.Length);
            if (offerCount > cards.Length)
                Debug.LogWarning("[GeoAds] " + (offerCount - cards.Length) + " offer(s) not shown: the carousel has only " + cards.Length + " card(s).");

            RebuildLayout();
        }

        private void PopulateCodeBuilt(GeoAdOffer[] offers)
        {
            EnsureBuilt();

            for (int i = clones.Count - 1; i >= 0; i--)
                if (clones[i] != null)
                    Destroy(clones[i]);
            clones.Clear();

            if (offers == null)
            {
                pageCount = 0;
                return;
            }

            foreach (GeoAdOffer offer in offers)
            {
                if (offer == null)
                    continue;
                var cardGo = new GameObject("GeoAdCard", typeof(RectTransform), typeof(Image));
                cardGo.layer = gameObject.layer;
                var card = cardGo.AddComponent<GeoAdCard>();
                card.Build(content, offer);
                clones.Add(cardGo);
            }

            pageCount = clones.Count;
            RebuildLayout();
        }

        // ---------------------------------------------------------------- paging

        public void OnBeginDrag(PointerEventData eventData)
        {
            InputLocked = true;
            if (content != null)
                content.DOKill(false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // ScrollRect does the work; this handler only exists so OnBeginDrag is called.
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            SnapToNearestPage();
            StartCoroutine(UnlockInputNextFrame());
        }

        private IEnumerator UnlockInputNextFrame()
        {
            yield return null; // the click of this same release is dispatched before we resume
            InputLocked = false;
        }

        private void SnapToNearestPage()
        {
            if (scroll == null || content == null || pageCount <= 1)
                return;

            float pageWidth = PageWidth();
            if (pageWidth <= 1f)
                return;

            int page = Mathf.Clamp(Mathf.RoundToInt(-content.anchoredPosition.x / pageWidth), 0, pageCount - 1);
            float target = -page * pageWidth;

            scroll.StopMovement();
            if (snapSeconds <= 0f || Mathf.Abs(content.anchoredPosition.x - target) < 0.5f)
            {
                Vector2 pos = content.anchoredPosition;
                pos.x = target;
                content.anchoredPosition = pos;
                return;
            }

            content.DOKill(true);
            content.DOAnchorPosX(target, snapSeconds).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        /// <summary>One page is one card: content width / card count (padding and spacing are 0).</summary>
        private float PageWidth()
        {
            if (content == null || pageCount <= 0)
                return 0f;
            return content.rect.width / pageCount;
        }

        private void RebuildLayout()
        {
            if (content == null)
                return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void OnEnable()
        {
            InputLocked = false;
        }

        private void OnDisable()
        {
            InputLocked = false;
            if (content != null)
                content.DOKill(false);
        }

        // ---------------------------------------------------------------- wiring

        private void ResolveContent()
        {
            if (content == null)
            {
                Transform found = transform.Find("Viewport/Content");
                if (found != null)
                    content = found as RectTransform;
            }
            if (scroll == null)
                scroll = GetComponent<ScrollRect>();
            if (content != null && scroll == null)
                scroll = content.GetComponentInParent<ScrollRect>();
        }

        private void HookCloseButton()
        {
            if (closeHooked || closeButton == null)
                return;
            closeButton.onClick.AddListener(Hide);
            closeHooked = true;
        }

        // ---------------------------------------------------------------- code-built fallback

        private void EnsureBuilt()
        {
            ResolveContent();
            if (canvas != null && content != null)
                return;

            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(640, 960);
                scaler.matchWidthOrHeight = 0f; // canvas is always 640 units wide -> cards fit full screen
                gameObject.AddComponent<GraphicRaycaster>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortOrder;

            if (GetComponent<Image>() == null)
            {
                var bg = gameObject.AddComponent<Image>();
                bg.color = new Color(0.04f, 0.02f, 0.13f, 0.55f);
            }
            if (GetComponent<ScrollRect>() == null)
            {
                var sr = gameObject.AddComponent<ScrollRect>();
                sr.horizontal = true;
                sr.vertical = false;
                sr.inertia = true;
                scroll = sr;
            }

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var vrect = (RectTransform)viewport.transform;
            vrect.SetParent(transform, false);
            vrect.anchorMin = Vector2.zero;
            vrect.anchorMax = Vector2.one;
            vrect.offsetMin = Vector2.zero;
            vrect.offsetMax = Vector2.zero;
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f); // transparent, but catches the drag

            var contentGo = new GameObject("Content", typeof(RectTransform));
            content = (RectTransform)contentGo.transform;
            content.SetParent(vrect, false);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = Vector2.zero;

            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.viewport = vrect;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
        }
    }
}