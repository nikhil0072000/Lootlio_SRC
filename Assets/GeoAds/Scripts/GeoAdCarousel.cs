using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GeoAds
{
    /// <summary>
    /// Ad carousel. Two ways to use it:
    /// - hand-made: Cards is filled in the Inspector with cards that already exist
    ///   under this object. Nothing is created, moved or resized; offers only fill
    ///   those cards and spare cards are hidden.
    /// - code-built (Cards empty): full-screen horizontal ScrollRect that builds its
    ///   own canvas, viewport, and cards entirely from code — no prefab or scene
    ///   wiring required.
    /// </summary>
    public class GeoAdCarousel : MonoBehaviour
    {
        [SerializeField] private GeoAdCard[] cards;
        [SerializeField] private Button closeButton; // optional

        private const int SortOrder = 900;   // above popups, below debugging overlays

        private Canvas canvas;
        private RectTransform content;
        private bool closeHooked;
        private readonly List<GameObject> builtCards = new List<GameObject>();

        private bool IsHandMade => cards != null && cards.Length > 0;

        public void Show()
        {
            if (!IsHandMade)
                EnsureBuilt();
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
            if (IsHandMade)
            {
                PopulateHandMade(offers);
                return;
            }

            EnsureBuilt();

            foreach (GameObject card in builtCards)
                if (card != null)
                    Destroy(card);
            builtCards.Clear();

            if (offers == null)
                return;

            float spacing = 40f;
            float x = 0f;
            foreach (GeoAdOffer offer in offers)
            {
                var cardGo = new GameObject("GeoAdCard_" + (offer.id ?? x.ToString("0")),
                    typeof(RectTransform), typeof(Image));
                var card = cardGo.AddComponent<GeoAdCard>();
                card.Build(content, offer);
                var rect = (RectTransform)cardGo.transform;
                // left-middle, so x is measured from the content's left edge
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(x + 20, 0);
                x += rect.sizeDelta.x + spacing;
                builtCards.Add(cardGo);
            }

            if (content != null)
                content.sizeDelta = new Vector2(x, content.sizeDelta.y);
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

            if (offerCount > cards.Length)
                Debug.LogWarning("[GeoAds] " + (offerCount - cards.Length) + " offer(s) not shown: the carousel has only " + cards.Length + " card(s).");
        }

        private void HookCloseButton()
        {
            if (closeHooked || closeButton == null)
                return;
            closeButton.onClick.AddListener(Hide);
            closeHooked = true;
        }

        private void EnsureBuilt()
        {
            if (canvas != null)
                return;

            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(640, 960); // main gameplay canvas matches this
                scaler.matchWidthOrHeight = 1f;
                gameObject.AddComponent<GraphicRaycaster>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortOrder;
            if (gameObject.scene.IsValid() == false)
            {
                // never parented — fine: root-level persistent object
            }

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var vrect = (RectTransform)viewport.transform;
            vrect.SetParent(transform, false);
            vrect.anchorMin = Vector2.zero;
            vrect.anchorMax = Vector2.one;
            vrect.offsetMin = Vector2.zero;
            vrect.offsetMax = Vector2.zero;

            var scroll = gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.viewport = vrect;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            content = (RectTransform)contentGo.transform;
            content.SetParent(vrect, false);
            content.anchorMin = new Vector2(0f, 0.5f);
            content.anchorMax = new Vector2(0f, 0.5f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = new Vector2(0, 260);
            scroll.content = content;
        }
    }
}
