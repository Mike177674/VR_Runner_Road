using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

[DefaultExecutionOrder(100)]
public class VRMenuUI : MonoBehaviour
{
    private static readonly Color PanelTint = new Color(0.04f, 0.07f, 0.11f, 0.56f);
    private static readonly Color PanelBorderTint = new Color(0.91f, 0.95f, 1f, 0.12f);
    private static readonly Color InfoTint = new Color(0.09f, 0.13f, 0.19f, 0.36f);
    private static readonly Color InfoBorderTint = new Color(0.91f, 0.95f, 1f, 0.10f);
    private static readonly Color ButtonTint = new Color(0.82f, 0.87f, 0.94f, 0.18f);
    private static readonly Color ButtonHoverTint = new Color(0.91f, 0.95f, 1f, 0.26f);
    private static readonly Color SelectedButtonTint = new Color(0.97f, 0.73f, 0.33f, 0.90f);
    private static readonly Color SelectedButtonHoverTint = new Color(1f, 0.79f, 0.41f, 0.96f);
    private static readonly Color MenuTextColor = new Color(0.96f, 0.97f, 0.99f, 1f);
    private static readonly Color MutedTextColor = new Color(0.84f, 0.88f, 0.93f, 1f);
    private static readonly Color StatValueColor = new Color(1f, 0.95f, 0.86f, 1f);
    private static readonly Color SelectedButtonTextColor = new Color(0.12f, 0.11f, 0.09f, 1f);
    private static readonly Color ShadowTextColor = new Color(0.01f, 0.02f, 0.03f, 0.72f);

    private const int UiLayer = 5;
    private const float CanvasWidth = 600f;
    private const float CanvasHeight = 388f;

    [SerializeField, Min(0.5f)] private float panelDistance = 1.6f;
    [SerializeField] private Vector3 panelOffset = new Vector3(0f, -0.08f, 0f);
    [SerializeField, Min(0.0005f)] private float panelWorldScale = 0.0015f;
    [SerializeField, Min(0.5f)] private float interactionDistance = 4f;

    private readonly List<MenuButtonView> buttons = new();
    private readonly Dictionary<Collider, MenuButtonView> buttonLookup = new();
    private readonly List<XRDisplaySubsystem> xrDisplays = new();

    private GameManager gameManager;
    private XRTracker xrTracker;
    private Camera mainCamera;
    private Canvas menuCanvas;
    private RectTransform canvasRect;
    private RectTransform layoutRoot;
    private Font uiFont;

    private Text titleText;
    private Text titleShadowText;
    private Text subtitleText;
    private RawImage infoBorder;
    private RawImage infoFill;
    private Text infoTitleText;
    private StatBlock[] statBlocks;
    private MenuButtonView[] modeButtons;
    private MenuButtonView actionButton;
    private MenuButtonView hoveredButton;
    private bool worldMenuActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<VRMenuUI>() != null)
        {
            return;
        }

        GameObject bootstrapObject = new GameObject(nameof(VRMenuUI), typeof(RectTransform));
        bootstrapObject.AddComponent<VRMenuUI>();
    }

    private void Awake()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildMenu();
    }

    private void Update()
    {
        ResolveReferences();
        UpdateWorldMenuState();

        if (!worldMenuActive || gameManager == null)
        {
            if (layoutRoot != null)
            {
                layoutRoot.gameObject.SetActive(false);
            }

            return;
        }

        if (layoutRoot != null)
        {
            layoutRoot.gameObject.SetActive(!gameManager.IsPlaying);
        }

        if (gameManager.IsPlaying)
        {
            hoveredButton = null;
            RefreshButtonStyles();
            return;
        }

        UpdateCanvasPlacement();
        UpdateMenuContents();
        UpdateInteraction();
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.SetWorldSpaceMenuPresentation(false);
        }
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindFirstObjectByType<GameManager>();
        }

        if (xrTracker == null)
        {
            xrTracker = FindFirstObjectByType<XRTracker>();
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (menuCanvas != null)
            {
                menuCanvas.worldCamera = mainCamera;
            }
        }
    }

    private void UpdateWorldMenuState()
    {
        bool shouldUseWorldMenu = IsVrDisplayRunning() && mainCamera != null && gameManager != null;
        if (worldMenuActive == shouldUseWorldMenu)
        {
            return;
        }

        worldMenuActive = shouldUseWorldMenu;
        if (gameManager != null)
        {
            gameManager.SetWorldSpaceMenuPresentation(worldMenuActive);
        }
    }

    private bool IsVrDisplayRunning()
    {
        xrDisplays.Clear();
        SubsystemManager.GetSubsystems(xrDisplays);

        for (int i = 0; i < xrDisplays.Count; i++)
        {
            if (xrDisplays[i] != null && xrDisplays[i].running)
            {
                return true;
            }
        }

        return false;
    }

    private void BuildMenu()
    {
        SetLayerRecursively(gameObject, UiLayer);

        menuCanvas = gameObject.AddComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.WorldSpace;
        menuCanvas.sortingOrder = 100;
        menuCanvas.pixelPerfect = false;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        gameObject.AddComponent<GraphicRaycaster>();

        canvasRect = gameObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
        canvasRect.localScale = Vector3.one * panelWorldScale;
        layoutRoot = CreateRect("Layout Root", canvasRect, new Vector2(CanvasWidth, CanvasHeight), Vector2.zero);

        CreatePanelBackground();
        CreateTitle();
        CreateModeButtons();
        CreateInfoPanel();
        CreateActionButton();
    }

    private void CreatePanelBackground()
    {
        RectTransform borderRect = CreateRect("Panel Border", layoutRoot, new Vector2(CanvasWidth, CanvasHeight), Vector2.zero);
        CreateSolidFill("Panel Fill", borderRect, new Vector2(CanvasWidth - 2f, CanvasHeight - 2f), Vector2.zero, PanelTint);
        RawImage border = borderRect.gameObject.AddComponent<RawImage>();
        border.texture = Texture2D.whiteTexture;
        border.color = PanelBorderTint;
        border.raycastTarget = false;
    }

    private void CreateTitle()
    {
        titleShadowText = CreateText(
            "Title Shadow",
            layoutRoot,
            new Vector2(552f, 42f),
            new Vector2(3f, 150f),
            34,
            FontStyle.Bold,
            ShadowTextColor,
            TextAnchor.MiddleCenter);

        titleText = CreateText(
            "Title",
            layoutRoot,
            new Vector2(552f, 42f),
            new Vector2(0f, 153f),
            34,
            FontStyle.Bold,
            MenuTextColor,
            TextAnchor.MiddleCenter);

        subtitleText = CreateText(
            "Subtitle",
            layoutRoot,
            new Vector2(552f, 24f),
            new Vector2(0f, 104f),
            18,
            FontStyle.Normal,
            MenuTextColor,
            TextAnchor.MiddleCenter);
    }

    private void CreateModeButtons()
    {
        modeButtons = new[]
        {
            CreateButton("Easy", new Vector2(-188f, 88f), new Vector2(176f, 44f), () => SelectMode(GameManager.GameMode.Easy)),
            CreateButton("Medium", new Vector2(0f, 88f), new Vector2(176f, 44f), () => SelectMode(GameManager.GameMode.Medium)),
            CreateButton("Hard", new Vector2(188f, 88f), new Vector2(176f, 44f), () => SelectMode(GameManager.GameMode.Hard))
        };

        modeButtons[0].Mode = GameManager.GameMode.Easy;
        modeButtons[1].Mode = GameManager.GameMode.Medium;
        modeButtons[2].Mode = GameManager.GameMode.Hard;
    }

    private void CreateInfoPanel()
    {
        RectTransform borderRect = CreateRect("Info Border", layoutRoot, new Vector2(552f, 132f), new Vector2(0f, -6f));
        infoBorder = borderRect.gameObject.AddComponent<RawImage>();
        infoBorder.texture = Texture2D.whiteTexture;
        infoBorder.color = InfoBorderTint;
        infoBorder.raycastTarget = false;

        RectTransform fillRect = CreateRect("Info Fill", borderRect, new Vector2(550f, 130f), Vector2.zero);
        infoFill = fillRect.gameObject.AddComponent<RawImage>();
        infoFill.texture = Texture2D.whiteTexture;
        infoFill.color = InfoTint;
        infoFill.raycastTarget = false;

        infoTitleText = CreateText(
            "Info Title",
            fillRect,
            new Vector2(512f, 28f),
            new Vector2(0f, 36f),
            22,
            FontStyle.Bold,
            MenuTextColor,
            TextAnchor.MiddleCenter);

        statBlocks = new[]
        {
            CreateStatBlock(fillRect, new Vector2(-176f, -16f)),
            CreateStatBlock(fillRect, new Vector2(0f, -16f)),
            CreateStatBlock(fillRect, new Vector2(176f, -16f))
        };
    }

    private void CreateActionButton()
    {
        actionButton = CreateButton("Start", new Vector2(0f, -150f), new Vector2(552f, 44f), HandleActionButton);
    }

    private void UpdateCanvasPlacement()
    {
        if (mainCamera == null)
        {
            return;
        }

        Transform headTransform = xrTracker != null && xrTracker.HeadTransform != null
            ? xrTracker.HeadTransform
            : mainCamera.transform;

        Vector3 flatForward = Vector3.ProjectOnPlane(headTransform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = Vector3.forward;
        }

        flatForward.Normalize();
        transform.position =
            headTransform.position
            + (flatForward * (panelDistance + panelOffset.z))
            + (headTransform.right * panelOffset.x)
            + (Vector3.up * panelOffset.y);
        transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
    }

    private void UpdateMenuContents()
    {
        if (gameManager.IsPreGame)
        {
            titleText.text = gameManager.GameTitle;
            titleShadowText.text = gameManager.GameTitle;
            subtitleText.gameObject.SetActive(false);
            SetModeButtonsVisible(true);

            GameManager.GameMode mode = gameManager.CurrentMode;
            infoTitleText.text = gameManager.GetModeSummary(mode);
            ConfigureInfoPanelHeight(132f, 130f, -6f);
            SetStatBlock(statBlocks[0], "Top Score", FormatScore(gameManager.GetHighScoreForMode(mode)), true);
            SetStatBlock(statBlocks[1], string.Empty, string.Empty, false);
            SetStatBlock(statBlocks[2], "Top Speed", FormatSpeed(gameManager.GetBestPeakSpeedForMode(mode)), true);

            actionButton.Label.text = $"Start {gameManager.GetModeLabel(mode)}";
        }
        else
        {
            titleText.text = "Run Failed";
            titleShadowText.text = "Run Failed";
            subtitleText.gameObject.SetActive(true);
            subtitleText.text = gameManager.GetModeSummary(gameManager.CurrentMode);
            SetModeButtonsVisible(false);

            ConfigureInfoPanelHeight(92f, 90f, 10f);
            SetStatBlock(statBlocks[0], "Score", FormatScore(gameManager.CurrentScore), true);
            SetStatBlock(statBlocks[1], "Top Score", FormatScore(gameManager.GetHighScoreForMode(gameManager.CurrentMode)), true);
            SetStatBlock(statBlocks[2], "Top Speed", FormatSpeed(gameManager.GetBestPeakSpeedForMode(gameManager.CurrentMode)), true);

            actionButton.Label.text = "Back To Modes";
        }

        RefreshButtonStyles();
    }

    private void ConfigureInfoPanelHeight(float borderHeight, float fillHeight, float yPosition)
    {
        infoBorder.rectTransform.sizeDelta = new Vector2(552f, borderHeight);
        infoBorder.rectTransform.anchoredPosition = new Vector2(0f, yPosition);
        infoFill.rectTransform.sizeDelta = new Vector2(550f, fillHeight);
    }

    private void SetModeButtonsVisible(bool visible)
    {
        for (int i = 0; i < modeButtons.Length; i++)
        {
            modeButtons[i].Root.SetActive(visible);
        }
    }

    private void UpdateInteraction()
    {
        hoveredButton = RaycastHoveredButton();
        RefreshButtonStyles();

        if (!WasMenuClickPressed() || hoveredButton == null)
        {
            return;
        }

        hoveredButton.ClickAction?.Invoke();
    }

    private MenuButtonView RaycastHoveredButton()
    {
        Transform source = xrTracker != null && xrTracker.RightHandTransform != null && xrTracker.RightHandTransform.gameObject.activeInHierarchy
            ? xrTracker.RightHandTransform
            : (xrTracker != null && xrTracker.HeadTransform != null ? xrTracker.HeadTransform : mainCamera != null ? mainCamera.transform : null);

        if (source == null)
        {
            return null;
        }

        int uiMask = 1 << UiLayer;
        if (!Physics.Raycast(source.position, source.forward, out RaycastHit hit, interactionDistance, uiMask, QueryTriggerInteraction.Collide))
        {
            return null;
        }

        return hit.collider != null && buttonLookup.TryGetValue(hit.collider, out MenuButtonView button)
            ? button
            : null;
    }

    private bool WasMenuClickPressed()
    {
        bool xrPressed = xrTracker != null && xrTracker.JumpPressedThisFrame;

        Keyboard keyboard = Keyboard.current;
        bool keyboardPressed = keyboard != null
            && ((keyboard.enterKey != null && keyboard.enterKey.wasPressedThisFrame)
                || (keyboard.spaceKey != null && keyboard.spaceKey.wasPressedThisFrame));

        Mouse mouse = Mouse.current;
        bool mousePressed = mouse != null && mouse.leftButton.wasPressedThisFrame;

        return xrPressed || keyboardPressed || mousePressed;
    }

    private void HandleActionButton()
    {
        if (gameManager == null)
        {
            return;
        }

        if (gameManager.IsPreGame)
        {
            gameManager.StartRun();
            return;
        }

        gameManager.ReturnToStartScreen();
    }

    private void SelectMode(GameManager.GameMode mode)
    {
        if (gameManager == null || !gameManager.IsPreGame)
        {
            return;
        }

        gameManager.SelectMode(mode);
    }

    private void RefreshButtonStyles()
    {
        if (gameManager == null)
        {
            return;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            MenuButtonView button = buttons[i];
            bool hovered = button == hoveredButton;
            bool selected = button.Mode.HasValue && button.Mode.Value == gameManager.CurrentMode && gameManager.IsPreGame;
            bool emphasized = button == actionButton || selected;

            button.Background.color = emphasized
                ? (hovered ? SelectedButtonHoverTint : SelectedButtonTint)
                : (hovered ? ButtonHoverTint : ButtonTint);

            button.Label.color = emphasized ? SelectedButtonTextColor : MenuTextColor;
            button.Label.fontStyle = emphasized ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    private MenuButtonView CreateButton(string label, Vector2 anchoredPosition, Vector2 size, Action clickAction)
    {
        RectTransform rect = CreateRect(label + " Button", layoutRoot, size, anchoredPosition);
        RawImage background = rect.gameObject.AddComponent<RawImage>();
        background.texture = Texture2D.whiteTexture;
        background.color = ButtonTint;
        background.raycastTarget = false;

        Text buttonText = CreateText(
            label + " Label",
            rect,
            size,
            Vector2.zero,
            20,
            FontStyle.Normal,
            MenuTextColor,
            TextAnchor.MiddleCenter);

        BoxCollider collider = rect.gameObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(size.x, size.y, 1f);

        MenuButtonView button = new MenuButtonView
        {
            Root = rect.gameObject,
            Background = background,
            Label = buttonText,
            Collider = collider,
            ClickAction = clickAction
        };

        buttons.Add(button);
        buttonLookup[collider] = button;
        SetLayerRecursively(rect.gameObject, UiLayer);
        return button;
    }

    private StatBlock CreateStatBlock(RectTransform parent, Vector2 anchoredPosition)
    {
        RectTransform root = CreateRect("Stat Block", parent, new Vector2(160f, 52f), anchoredPosition);

        Text label = CreateText(
            "Label",
            root,
            new Vector2(160f, 20f),
            new Vector2(0f, 10f),
            16,
            FontStyle.Normal,
            MutedTextColor,
            TextAnchor.MiddleCenter);

        Text value = CreateText(
            "Value",
            root,
            new Vector2(160f, 30f),
            new Vector2(0f, -10f),
            24,
            FontStyle.Bold,
            StatValueColor,
            TextAnchor.MiddleCenter);

        return new StatBlock
        {
            Root = root.gameObject,
            Label = label,
            Value = value
        };
    }

    private static void SetStatBlock(StatBlock statBlock, string label, string value, bool visible)
    {
        if (statBlock == null || statBlock.Root == null)
        {
            return;
        }

        statBlock.Root.SetActive(visible);
        if (!visible)
        {
            return;
        }

        statBlock.Label.text = label;
        statBlock.Value.text = value;
    }

    private RawImage CreateSolidFill(string name, RectTransform parent, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        RectTransform rect = CreateRect(name, parent, size, anchoredPosition);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = Texture2D.whiteTexture;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private RectTransform CreateRect(string name, RectTransform parent, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        SetLayerRecursively(child, UiLayer);
        return rect;
    }

    private Text CreateText(
        string name,
        RectTransform parent,
        Vector2 size,
        Vector2 anchoredPosition,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        TextAnchor alignment)
    {
        RectTransform rect = CreateRect(name, parent, size, anchoredPosition);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }

        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
        {
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }
    }

    private static string FormatScore(int score)
    {
        return score.ToString();
    }

    private static string FormatSpeed(float speed)
    {
        return speed > 0f ? speed.ToString("0.0") : "--";
    }

    private sealed class MenuButtonView
    {
        public GameObject Root;
        public RawImage Background;
        public Text Label;
        public Collider Collider;
        public Action ClickAction;
        public GameManager.GameMode? Mode;
    }

    private sealed class StatBlock
    {
        public GameObject Root;
        public Text Label;
        public Text Value;
    }
}
