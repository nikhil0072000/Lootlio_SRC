using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Playlio.Auth
{
    /// <summary>
    /// Full-screen neon Sign In / Sign Up overlay, built entirely in code (no scene or prefab wiring).
    /// Blocks the game behind it until the player signs in, signs up or picks "Play as guest".
    /// </summary>
    public sealed class PlaylioAuthScreen : MonoBehaviour
    {
        private const int SortingOrder = 1000;
        private const int MinPasswordLength = 10;   // matches Better Auth minPasswordLength
        private const int MaxPasswordLength = 128;  // matches Better Auth maxPasswordLength
        private const float CloseDelaySeconds = 0.8f;
        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
        private static readonly WaitForSecondsRealtime EventSystemCheck = new WaitForSecondsRealtime(0.5f);

        public event Action Closed;

        private PlaylioAuthService service;
        private bool signUpMode;
        private bool busy;

        private Button signInTab;
        private Button signUpTab;
        private TMP_InputField nameField;
        private TMP_InputField emailField;
        private TMP_InputField passwordField;
        private Button primaryButton;
        private TextMeshProUGUI primaryLabel;
        private Button guestButton;
        private TextMeshProUGUI statusLabel;

        /// <summary>Creates the overlay under parent (a DontDestroyOnLoad object) and shows it.</summary>
        public static PlaylioAuthScreen Show(Transform parent, PlaylioAuthService authService)
        {
            var root = new GameObject("PlaylioAuthScreen", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var screen = root.AddComponent<PlaylioAuthScreen>();
            screen.service = authService;
            screen.Build();
            screen.SetMode(false);
            return screen;
        }

        private void OnEnable()
        {
            StartCoroutine(KeepEventSystem());
        }

        // The overlay survives scene changes; make sure some EventSystem exists so it stays clickable.
        private IEnumerator KeepEventSystem()
        {
            while (true)
            {
                if (EventSystem.current == null)
                {
                    var eventSystem = new GameObject("EventSystem (PlaylioAuth)");
                    eventSystem.AddComponent<EventSystem>();
                    eventSystem.AddComponent<StandaloneInputModule>();
                }
                yield return EventSystemCheck;
            }
        }

        // ---------------- UI building ----------------

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // full-screen dark backdrop; also blocks taps on the game behind
            var backdrop = gameObject.AddComponent<Image>();
            backdrop.color = new Color(PlaylioNeonPalette.Canvas.r, PlaylioNeonPalette.Canvas.g, PlaylioNeonPalette.Canvas.b, 0.97f);
            backdrop.raycastTarget = true;

            RectTransform panel = CreatePanel(transform);

            TextMeshProUGUI title = CreateText(panel, "Playlio", 96, FontStyles.Bold, PlaylioNeonPalette.Cyan);
            SetHeight(title.gameObject, 130);

            RectTransform tabs = CreateRow(panel, 110);
            signInTab = CreateButton(tabs, "Sign In", out _);
            signUpTab = CreateButton(tabs, "Sign Up", out _);
            signInTab.onClick.AddListener(() => SetMode(false));
            signUpTab.onClick.AddListener(() => SetMode(true));

            nameField = CreateInput(panel, "Display name", TMP_InputField.ContentType.Standard);
            emailField = CreateInput(panel, "Email", TMP_InputField.ContentType.EmailAddress);
            passwordField = CreateInput(panel, "Password (10+ characters)", TMP_InputField.ContentType.Password);
            passwordField.characterLimit = MaxPasswordLength;

            primaryButton = CreateButton(panel, "Sign In", out primaryLabel);
            SetHeight(primaryButton.gameObject, 130);
            primaryButton.onClick.AddListener(Submit);

            statusLabel = CreateText(panel, string.Empty, 38, FontStyles.Normal, PlaylioNeonPalette.TextSecondary);
            SetHeight(statusLabel.gameObject, 110);

            guestButton = CreateButton(panel, "Play as guest", out TextMeshProUGUI guestLabel);
            SetHeight(guestButton.gameObject, 100);
            StyleButton(guestButton, guestLabel, PlaylioNeonPalette.Raised, PlaylioNeonPalette.TextSecondary);
            guestButton.onClick.AddListener(Close);
        }

        private static RectTransform CreatePanel(Transform parent)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(Outline));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 0f);

            go.GetComponent<Image>().color = PlaylioNeonPalette.Raised;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = PlaylioNeonPalette.Cyan;
            outline.effectDistance = new Vector2(3f, -3f);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(60, 60, 50, 50);
            layout.spacing = 26f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        private static RectTransform CreateRow(Transform parent, float height)
        {
            var go = new GameObject("Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            SetHeight(go, height);
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string text, float size, FontStyles style, Color color)
        {
            GameObject go = TMP_DefaultControls.CreateText(new TMP_DefaultControls.Resources());
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(Transform parent, string text, out TextMeshProUGUI label)
        {
            GameObject go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            go.transform.SetParent(parent, false);
            var button = go.GetComponent<Button>();
            label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = text;
                label.fontSize = 44;
                label.fontStyle = FontStyles.Bold;
            }
            StyleButton(button, label, PlaylioNeonPalette.Cyan, PlaylioNeonPalette.Canvas);
            SetHeight(go, 110);
            return button;
        }

        private static void StyleButton(Button button, TextMeshProUGUI label, Color fill, Color textColor)
        {
            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = Color.white;   // tint comes from the ColorBlock

            ColorBlock colors = button.colors;
            colors.normalColor = fill;
            colors.highlightedColor = Color.Lerp(fill, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(fill, Color.black, 0.18f);
            colors.selectedColor = fill;
            colors.disabledColor = new Color(0.22f, 0.25f, 0.32f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            if (label != null)
                label.color = textColor;
        }

        private static TMP_InputField CreateInput(Transform parent, string placeholder, TMP_InputField.ContentType contentType)
        {
            GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.transform.SetParent(parent, false);
            SetHeight(go, 110);

            var background = go.GetComponent<Image>();
            if (background != null)
                background.color = PlaylioNeonPalette.Elevated;

            var field = go.GetComponent<TMP_InputField>();
            field.contentType = contentType;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = MaxPasswordLength;

            if (field.textComponent != null)
            {
                field.textComponent.fontSize = 42;
                field.textComponent.color = PlaylioNeonPalette.TextPrimary;
            }
            if (field.placeholder is TMP_Text hint)
            {
                hint.text = placeholder;
                hint.fontSize = 42;
                hint.color = new Color(PlaylioNeonPalette.TextSecondary.r, PlaylioNeonPalette.TextSecondary.g, PlaylioNeonPalette.TextSecondary.b, 0.6f);
            }
            field.ForceLabelUpdate();
            return field;
        }

        private static void SetHeight(GameObject go, float height)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null)
                element = go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }

        // ---------------- behaviour ----------------

        private void SetMode(bool signUp)
        {
            signUpMode = signUp;
            nameField.gameObject.SetActive(signUp);
            primaryLabel.text = signUp ? "Create Account" : "Sign In";

            StyleButton(signInTab, signInTab.GetComponentInChildren<TextMeshProUGUI>(true),
                signUp ? PlaylioNeonPalette.Elevated : PlaylioNeonPalette.Cyan,
                signUp ? PlaylioNeonPalette.TextSecondary : PlaylioNeonPalette.Canvas);
            StyleButton(signUpTab, signUpTab.GetComponentInChildren<TextMeshProUGUI>(true),
                signUp ? PlaylioNeonPalette.Magenta : PlaylioNeonPalette.Elevated,
                signUp ? PlaylioNeonPalette.Canvas : PlaylioNeonPalette.TextSecondary);
            StyleButton(primaryButton, primaryLabel,
                signUp ? PlaylioNeonPalette.Magenta : PlaylioNeonPalette.Cyan, PlaylioNeonPalette.Canvas);

            SetStatus(signUp ? "Create your Playlio account." : "Welcome to Playlio", PlaylioNeonPalette.TextSecondary);
        }

        private void Submit()
        {
            if (busy || !Validate())
                return;

            SetBusy(true);
            string email = emailField.text.Trim();
            string password = passwordField.text;

            if (signUpMode)
            {
                SetStatus("Creating account…", PlaylioNeonPalette.Cyan);
                service.SignUp(nameField.text.Trim(), email, password, OnAuthResult);
            }
            else
            {
                SetStatus("Signing in…", PlaylioNeonPalette.Cyan);
                service.SignIn(email, password, OnAuthResult);
            }
        }

        private void OnAuthResult(bool ok, string message)
        {
            if (this == null)
                return;   // screen was closed while the request ran

            if (ok)
            {
                passwordField.text = string.Empty;
                SetStatus(message, PlaylioNeonPalette.Lime);
                StartCoroutine(CloseAfterDelay());
            }
            else
            {
                SetBusy(false);
                SetStatus(message, PlaylioNeonPalette.Red);
            }
        }

        private bool Validate()
        {
            if (signUpMode && string.IsNullOrWhiteSpace(nameField.text))
            {
                SetStatus("Enter your display name.", PlaylioNeonPalette.Orange);
                return false;
            }
            string email = emailField.text.Trim();
            int at = email.IndexOf('@');
            if (at <= 0 || at == email.Length - 1)
            {
                SetStatus("Enter a valid email address.", PlaylioNeonPalette.Orange);
                return false;
            }
            int length = passwordField.text.Length;
            if (length < MinPasswordLength)
            {
                SetStatus("Password must contain at least 10 characters.", PlaylioNeonPalette.Orange);
                return false;
            }
            if (length > MaxPasswordLength)
            {
                SetStatus("Password must be 128 characters or fewer.", PlaylioNeonPalette.Orange);
                return false;
            }
            return true;
        }

        private void SetBusy(bool value)
        {
            busy = value;
            signInTab.interactable = !value;
            signUpTab.interactable = !value;
            primaryButton.interactable = !value;
            guestButton.interactable = !value;
        }

        private void SetStatus(string message, Color color)
        {
            statusLabel.text = message;
            statusLabel.color = color;
        }

        private IEnumerator CloseAfterDelay()
        {
            yield return new WaitForSecondsRealtime(CloseDelaySeconds);
            Close();
        }

        private void Close()
        {
            Closed?.Invoke();
            Destroy(gameObject);
        }
    }
}
