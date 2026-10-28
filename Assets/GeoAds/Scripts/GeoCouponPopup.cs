using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GeoAds
{
    /// <summary>
    /// Coupon popup for internal (merchant) offers. All visuals live in
    /// Assets/GeoAds/Prefabs/GeoCouponPopup.prefab (made by Tools > GeoAds > Build Coupon Popup Prefab)
    /// and can be restyled freely in the editor; this script only fills the assigned labels.
    /// The carousel registers the prefab (its "Coupon Popup Prefab" slot); one instance is
    /// created on first use and reused after that.
    /// </summary>
    public class GeoCouponPopup : MonoBehaviour
    {
        [Header("Labels (filled per offer; empty offer values keep the prefab text)")]
        [SerializeField] private TextMeshProUGUI storeLabel;
        [SerializeField] private TextMeshProUGUI discountLabel;
        [SerializeField] private TextMeshProUGUI codeLabel;

        [Header("Buttons")]
        [SerializeField] private Button copyButton;
        [SerializeField] private Button closeButton;
        [Tooltip("Optional: the dark full-screen area behind the panel. Tapping it closes the popup.")]
        [SerializeField] private Button dimmerButton;

        [Header("Copied feedback")]
        [Tooltip("Shown after Copy is pressed, hidden again after Copied Seconds.")]
        [SerializeField] private GameObject copiedFeedback;
        [SerializeField] private float copiedSeconds = 1.5f;

        private static GeoCouponPopup prefab;
        private static GeoCouponPopup instance;
        private static bool warnedMissingPrefab;

        private string currentCode;
        private string defaultStore;
        private string defaultDiscount;
        private Coroutine copiedRoutine;

        /// <summary>Remembers which prefab to use. Called by GeoAdCarousel from its Inspector slot.</summary>
        public static void Register(GeoCouponPopup popupPrefab)
        {
            if (popupPrefab != null)
                prefab = popupPrefab;
        }

        /// <summary>Shows the popup for this offer, creating the instance on first use.</summary>
        public static void Show(string couponCode, GeoAdOffer offer)
        {
            if (instance == null)
            {
                if (prefab == null)
                {
                    // no UI without the prefab - still give the player the code
                    if (!string.IsNullOrEmpty(couponCode))
                        GUIUtility.systemCopyBuffer = couponCode;
                    if (!warnedMissingPrefab)
                    {
                        Debug.LogWarning("[GeoAds] Coupon popup prefab is not assigned (GeoAdCarousel > Coupon Popup Prefab). "
                            + "The code was copied to the clipboard instead. Run Tools > GeoAds > Build Coupon Popup Prefab.");
                        warnedMissingPrefab = true;
                    }
                    return;
                }

                instance = Instantiate(prefab);
                instance.name = prefab.name;
            }

            instance.Fill(couponCode, offer);
            instance.gameObject.SetActive(true);
        }

        /// <summary>Hides the popup if it is open.</summary>
        public static void Close()
        {
            if (instance != null)
                instance.gameObject.SetActive(false);
        }

        // Enter Play Mode without domain reload keeps statics; start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            prefab = null;
            instance = null;
            warnedMissingPrefab = false;
        }

        private void Awake()
        {
            defaultStore = storeLabel != null ? storeLabel.text : string.Empty;
            defaultDiscount = discountLabel != null ? discountLabel.text : string.Empty;

            if (copyButton != null)
                copyButton.onClick.AddListener(CopyCode);
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            if (dimmerButton != null)
                dimmerButton.onClick.AddListener(Close);

            if (copiedFeedback != null)
                copiedFeedback.SetActive(false);
        }

        private void OnDisable()
        {
            if (copiedRoutine != null)
            {
                StopCoroutine(copiedRoutine);
                copiedRoutine = null;
            }
            if (copiedFeedback != null)
                copiedFeedback.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Fill(string couponCode, GeoAdOffer offer)
        {
            currentCode = couponCode;

            if (storeLabel != null)
                storeLabel.text = offer != null && !string.IsNullOrEmpty(offer.storeName) ? offer.storeName : defaultStore;

            if (discountLabel != null)
                discountLabel.text = offer != null && offer.coupon != null && !string.IsNullOrEmpty(offer.coupon.discountText)
                    ? offer.coupon.discountText
                    : defaultDiscount;

            if (codeLabel != null && !string.IsNullOrEmpty(couponCode))
                codeLabel.text = couponCode;

            if (copiedFeedback != null)
                copiedFeedback.SetActive(false);
        }

        private void CopyCode()
        {
            if (string.IsNullOrEmpty(currentCode))
                return;

            GUIUtility.systemCopyBuffer = currentCode;

            if (copiedFeedback == null)
                return;
            if (copiedRoutine != null)
                StopCoroutine(copiedRoutine);
            copiedRoutine = StartCoroutine(ShowCopied());
        }

        private IEnumerator ShowCopied()
        {
            copiedFeedback.SetActive(true);
            yield return new WaitForSecondsRealtime(copiedSeconds); // works while the game is paused
            copiedFeedback.SetActive(false);
            copiedRoutine = null;
        }
    }
}
