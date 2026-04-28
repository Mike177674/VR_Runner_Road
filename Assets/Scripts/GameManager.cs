using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class GameManager : MonoBehaviour
{
    private const string HighScoreKeyPrefix = "HighScoreValue.";
    private const string PeakSpeedKeyPrefix = "PeakSpeedValue.";
    private const string SelectedModeKey = "SelectedDifficultyMode";

    private enum GameState
    {
        PreGame,
        Playing,
        GameOver
    }

    public enum GameMode
    {
        Easy,
        Medium,
        Hard
    }

    private enum SpeedCurveType
    {
        SmoothStep,
        Exponential,
        LateBoost
    }

    private struct DifficultyProfile
    {
        public DifficultyProfile(
            string label,
            SpeedCurveType speedCurve,
            float startWorldSpeed,
            float softCapWorldSpeed,
            float speedRampDuration,
            float lateBoostStartTime,
            float lateBoostDuration,
            float overflowStartTime,
            float overflowGrowthRate,
            float overflowGrowthScale,
            float scoreMultiplier,
            float jumpVelocity,
            float highSpeedJumpVelocity,
            int minEasySectionGap)
        {
            Label = label;
            SpeedCurve = speedCurve;
            StartWorldSpeed = startWorldSpeed;
            SoftCapWorldSpeed = softCapWorldSpeed;
            SpeedRampDuration = speedRampDuration;
            LateBoostStartTime = lateBoostStartTime;
            LateBoostDuration = lateBoostDuration;
            OverflowStartTime = overflowStartTime;
            OverflowGrowthRate = overflowGrowthRate;
            OverflowGrowthScale = overflowGrowthScale;
            ScoreMultiplier = scoreMultiplier;
            JumpVelocity = jumpVelocity;
            HighSpeedJumpVelocity = highSpeedJumpVelocity;
            MinEasySectionGap = minEasySectionGap;
        }

        public string Label { get; }
        public SpeedCurveType SpeedCurve { get; }
        public float StartWorldSpeed { get; }
        public float SoftCapWorldSpeed { get; }
        public float SpeedRampDuration { get; }
        public float LateBoostStartTime { get; }
        public float LateBoostDuration { get; }
        public float OverflowStartTime { get; }
        public float OverflowGrowthRate { get; }
        public float OverflowGrowthScale { get; }
        public float ScoreMultiplier { get; }
        public float JumpVelocity { get; }
        public float HighSpeedJumpVelocity { get; }
        public int MinEasySectionGap { get; }
    }

    private static readonly GameMode[] MenuModes =
    {
        GameMode.Easy,
        GameMode.Medium,
        GameMode.Hard
    };

    private static readonly Color PanelTint = new Color(0.04f, 0.07f, 0.11f, 0.56f);
    private static readonly Color PanelBorderTint = new Color(0.91f, 0.95f, 1f, 0.12f);
    private static readonly Color InfoTint = new Color(0.09f, 0.13f, 0.19f, 0.36f);
    private static readonly Color InfoBorderTint = new Color(0.91f, 0.95f, 1f, 0.10f);
    private static readonly Color ButtonTint = new Color(0.82f, 0.87f, 0.94f, 0.18f);
    private static readonly Color ButtonHoverTint = new Color(0.91f, 0.95f, 1f, 0.26f);
    private static readonly Color ButtonActiveTint = new Color(0.96f, 0.98f, 1f, 0.32f);
    private static readonly Color SelectedButtonTint = new Color(0.97f, 0.73f, 0.33f, 0.90f);
    private static readonly Color SelectedButtonHoverTint = new Color(1f, 0.79f, 0.41f, 0.96f);
    private static readonly Color SelectedButtonActiveTint = new Color(0.90f, 0.64f, 0.27f, 0.96f);
    private static readonly Color MenuTextColor = new Color(0.96f, 0.97f, 0.99f, 1f);
    private static readonly Color MutedTextColor = new Color(0.84f, 0.88f, 0.93f, 1f);
    private static readonly Color StatValueColor = new Color(1f, 0.95f, 0.86f, 1f);
    private static readonly Color SelectedButtonTextColor = new Color(0.12f, 0.11f, 0.09f, 1f);
    private static readonly Color ShadowTextColor = new Color(0.01f, 0.02f, 0.03f, 0.72f);

    public static GameManager Instance { get; private set; }

    public bool IsPlaying => state == GameState.Playing;
    public bool IsPreGame => state == GameState.PreGame;
    public bool IsXrActive => isXrActive;
    public bool IsXrSessionDetected => xrSessionDetected;
    public string GameTitle => gameTitle;
    public int CurrentScore => CalculateScore(currentRunTime);
    public int HighScore => GetHighScore(CurrentMode);
    public float CurrentWorldSpeed => EvaluateWorldSpeed(currentRunTime, GetActiveProfile());
    public float CurrentWorldSpeedProgress
    {
        get
        {
            DifficultyProfile profile = GetActiveProfile();
            if (profile.SoftCapWorldSpeed <= profile.StartWorldSpeed)
            {
                return 0f;
            }

            return Mathf.InverseLerp(
                profile.StartWorldSpeed,
                profile.SoftCapWorldSpeed,
                Mathf.Min(CurrentWorldSpeed, profile.SoftCapWorldSpeed));
        }
    }

    public float CurrentJumpVelocity => GetActiveProfile().JumpVelocity;
    public float CurrentHighSpeedJumpVelocity => GetActiveProfile().HighSpeedJumpVelocity;
    public int CurrentMinEasySectionGap => GetActiveProfile().MinEasySectionGap;
    public GameMode CurrentMode => state == GameState.PreGame ? selectedMode : activeMode;

    [Header("Menu")]
    [SerializeField] private string gameTitle = "VR Runner Road";
    [SerializeField, Min(460f)] private float menuWidth = 600f;
    [SerializeField, Min(320f)] private float menuHeight = 388f;
    [SerializeField, Min(20)] private int titleFontSize = 34;
    [SerializeField, Min(16)] private int bodyFontSize = 18;
    [SerializeField, Min(16)] private int buttonFontSize = 20;
    [SerializeField, Min(16)] private int hudFontSize = 22;

    [Header("Scoring")]
    [SerializeField, Min(1f)] private float baseScorePerSecond = 10f;

    [Header("XR")]
    [SerializeField] private string xrPlayerRootName = "PlayerArmature";
    [SerializeField] private string xrRigName = "XRRig";
    [SerializeField] private float xrHeightOffset = 0.1f;
    [SerializeField, Min(0.5f)] private float xrMenuDistance = 2f;
    [SerializeField] private float xrMenuVerticalOffset = -0.1f;
    [SerializeField] private bool enableLegacyMenus = false;

    private GameState state = GameState.PreGame;
    private GameMode selectedMode = GameMode.Medium;
    private GameMode activeMode = GameMode.Medium;
    private float currentRunTime;
    private float runPeakSpeed;
    private GUIStyle titleStyle;
    private GUIStyle titleShadowStyle;
    private GUIStyle bodyStyle;
    private GUIStyle cardTitleStyle;
    private GUIStyle statLabelStyle;
    private GUIStyle statValueStyle;
    private GUIStyle buttonStyle;
    private GUIStyle selectedButtonStyle;
    private GUIStyle hudStyle;
    private readonly System.Collections.Generic.List<XRDisplaySubsystem> xrDisplays = new();
    private InputAction menuConfirmAction;
    private bool isXrActive;
    private bool xrSessionDetected;
    private bool xrSetupApplied;
    private bool xrVisualsHidden;
    private Transform playerRoot;
    private Transform xrRig;
    private Transform xrHead;
    private Renderer[] playerRenderers;
    private Animator[] playerAnimators;
    private GameObject xrMenuRoot;
    private TextMesh xrMenuTitle;
    private TextMesh xrMenuStatus;
    private TextMesh xrMenuAction;
    private GUIStyle hudShadowStyle;
    private bool useWorldSpaceMenuPresentation;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        selectedMode = LoadSelectedMode();
        activeMode = selectedMode;
        runPeakSpeed = GetProfile(activeMode).StartWorldSpeed;
        ApplyState();
    }

    private void Start()
    {
        ApplyState();
    }

    private void OnEnable()
    {
        EnsureMenuConfirmAction();
    }

    private void Update()
    {
        RefreshXrState();
        UpdateCursorState();
        UpdateMenuInput();
        UpdateXrMenuState();

        if (state == GameState.Playing)
        {
            currentRunTime += Time.deltaTime;
            runPeakSpeed = Mathf.Max(runPeakSpeed, CurrentWorldSpeed);
        }
    }

    private void LateUpdate()
    {
        if (!isXrActive)
        {
            return;
        }

        AlignXrRigToPlayer();
        UpdateXrMenuPose();
    }

    public void StartRun()
    {
        activeMode = selectedMode;
        SaveSelectedMode(selectedMode);
        currentRunTime = 0f;
        runPeakSpeed = GetProfile(activeMode).StartWorldSpeed;
        state = GameState.Playing;
        ApplyState();
    }

    public void TriggerGameOver()
    {
        if (state != GameState.Playing)
        {
            return;
        }

        state = GameState.GameOver;

        int finalScore = CurrentScore;
        if (finalScore > GetHighScore(activeMode))
        {
            SaveHighScore(activeMode, finalScore);
        }

        if (runPeakSpeed > GetBestPeakSpeed(activeMode))
        {
            SaveBestPeakSpeed(activeMode, runPeakSpeed);
        }

        ApplyState();
    }

    public void Restart()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(activeScene.path))
        {
            SceneManager.LoadScene(activeScene.path);
            return;
        }

        if (!string.IsNullOrEmpty(activeScene.name))
        {
            SceneManager.LoadScene(activeScene.name);
            return;
        }

        Debug.LogError("GameManager: Unable to restart because the active scene has no valid name/path.");
    }

    public void ReturnToStartScreen()
    {
        Restart();
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        DisableMenuConfirmAction();
        SetPlayerVisualsEnabled(true);

        if (xrMenuRoot != null)
        {
            Destroy(xrMenuRoot);
            xrMenuRoot = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnGUI()
    {
        if (!enableLegacyMenus)
        {
            return;
        }

        if (isXrActive || useWorldSpaceMenuPresentation || xrSessionDetected)
        {
            return;
        }

        EnsureGuiStyles();

        if (state == GameState.Playing)
        {
            DrawScoreHud();
            return;
        }

        if (state == GameState.PreGame)
        {
            DrawStartMenu();
            return;
        }

        DrawFailureMenu();
    }

    private void DrawStartMenu()
    {
        Rect panelRect = GetMenuRect();
        DrawPanel(panelRect, PanelTint, PanelBorderTint);

        DifficultyProfile profile = GetProfile(selectedMode);
        Rect contentRect = GetMenuContentRect(panelRect);
        float y = contentRect.y + 20f;

        DrawLabelWithShadow(new Rect(contentRect.x, y, contentRect.width, 42f), gameTitle, titleStyle, titleShadowStyle);
        y += 58f;

        DrawModeSelectionButtons(contentRect.x, y, contentRect.width);
        y += 62f;

        Rect infoRect = new Rect(contentRect.x, y, contentRect.width, 132f);
        DrawPanel(infoRect, InfoTint, InfoBorderTint);

        Rect infoContentRect = InsetRect(infoRect, 20f, 18f);
        GUI.Label(
            new Rect(infoContentRect.x, infoContentRect.y, infoContentRect.width, 28f),
            $"{profile.Label} | {FormatScoreMultiplier(profile.ScoreMultiplier)}",
            cardTitleStyle);

        float statY = infoContentRect.y + 46f;
        float statWidth = (infoContentRect.width - 16f) * 0.5f;
        DrawStatBlock(
            new Rect(infoContentRect.x, statY, statWidth, 52f),
            "Top Score",
            FormatScore(GetHighScore(selectedMode)));
        DrawStatBlock(
            new Rect(infoContentRect.x + statWidth + 16f, statY, statWidth, 52f),
            "Top Speed",
            FormatSpeedStat(GetBestPeakSpeed(selectedMode)));

        Rect buttonRect = new Rect(contentRect.x, panelRect.y + panelRect.height - 62f, contentRect.width, 44f);
        if (DrawMenuButton(buttonRect, $"Start {profile.Label}", selectedButtonStyle))
        {
            StartRun();
        }
    }

    private void DrawFailureMenu()
    {
        Rect panelRect = GetMenuRect();
        DrawPanel(panelRect, PanelTint, PanelBorderTint);

        Rect contentRect = GetMenuContentRect(panelRect);
        float y = contentRect.y + 24f;

        DrawLabelWithShadow(new Rect(contentRect.x, y, contentRect.width, 40f), "Run Failed", titleStyle, titleShadowStyle);
        y += 50f;
        DifficultyProfile profile = GetProfile(activeMode);
        GUI.Label(
            new Rect(contentRect.x, y, contentRect.width, 24f),
            $"{profile.Label} | {FormatScoreMultiplier(profile.ScoreMultiplier)}",
            bodyStyle);
        y += 36f;

        Rect infoRect = new Rect(contentRect.x, y, contentRect.width, 92f);
        DrawPanel(infoRect, InfoTint, InfoBorderTint);

        Rect infoContentRect = InsetRect(infoRect, 20f, 18f);
        const float statSpacing = 12f;
        float statWidth = (infoContentRect.width - (statSpacing * 2f)) / 3f;
        DrawStatBlock(
            new Rect(infoContentRect.x, infoContentRect.y, statWidth, 52f),
            "Score",
            FormatScore(CurrentScore));
        DrawStatBlock(
            new Rect(infoContentRect.x + statWidth + statSpacing, infoContentRect.y, statWidth, 52f),
            "Top Score",
            FormatScore(GetHighScore(activeMode)));
        DrawStatBlock(
            new Rect(infoContentRect.x + ((statWidth + statSpacing) * 2f), infoContentRect.y, statWidth, 52f),
            "Top Speed",
            FormatSpeedStat(GetBestPeakSpeed(activeMode)));

        Rect buttonRect = new Rect(contentRect.x, panelRect.y + panelRect.height - 62f, contentRect.width, 44f);
        if (DrawMenuButton(buttonRect, "Back To Modes", selectedButtonStyle))
        {
            ReturnToStartScreen();
        }
    }

    private void DrawScoreHud()
    {
        DrawHudLine(20f, 20f, $"Score: {FormatScore(CurrentScore)}");
        DrawHudLine(20f, 50f, $"Best: {FormatScore(GetHighScore(activeMode))}");
        DrawHudLine(20f, 80f, $"Mode: {GetProfile(activeMode).Label}");
        DrawHudLine(20f, 110f, $"Speed: {CurrentWorldSpeed:0.0}");
    }

    private void DrawModeSelectionButtons(float x, float y, float width)
    {
        const float spacing = 12f;
        float buttonWidth = (width - (spacing * (MenuModes.Length - 1))) / MenuModes.Length;

        for (int i = 0; i < MenuModes.Length; i++)
        {
            GameMode mode = MenuModes[i];
            DifficultyProfile profile = GetProfile(mode);
            bool isSelected = mode == selectedMode;
            Rect buttonRect = new Rect(x + (buttonWidth + spacing) * i, y, buttonWidth, 44f);
            GUIStyle style = isSelected ? selectedButtonStyle : buttonStyle;

            if (DrawMenuButton(buttonRect, profile.Label, style))
            {
                SelectMode(mode);
            }
        }
    }

    public void SelectMode(GameMode mode)
    {
        if (selectedMode == mode)
        {
            return;
        }

        selectedMode = mode;
        SaveSelectedMode(mode);
    }

    public string GetModeSummary(GameMode mode)
    {
        DifficultyProfile profile = GetProfile(mode);
        return $"{profile.Label} | {FormatScoreMultiplier(profile.ScoreMultiplier)}";
    }

    public int GetHighScoreForMode(GameMode mode)
    {
        return GetHighScore(mode);
    }

    public float GetBestPeakSpeedForMode(GameMode mode)
    {
        return GetBestPeakSpeed(mode);
    }

    public string GetModeLabel(GameMode mode)
    {
        return GetProfile(mode).Label;
    }

    public void SetWorldSpaceMenuPresentation(bool worldSpaceActive)
    {
        useWorldSpaceMenuPresentation = worldSpaceActive;
        if (xrMenuRoot != null)
        {
            xrMenuRoot.SetActive(!worldSpaceActive && state != GameState.Playing);
        }
    }

    private void EnsureGuiStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = titleFontSize,
            fontStyle = FontStyle.Bold
        };
        ApplyTextColor(titleStyle, MenuTextColor);

        titleShadowStyle = new GUIStyle(titleStyle);
        ApplyTextColor(titleShadowStyle, ShadowTextColor);

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = bodyFontSize
        };
        ApplyTextColor(bodyStyle, MenuTextColor);

        cardTitleStyle = new GUIStyle(bodyStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = bodyFontSize + 4,
            fontStyle = FontStyle.Bold
        };
        ApplyTextColor(cardTitleStyle, MenuTextColor);

        statLabelStyle = new GUIStyle(bodyStyle)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = Mathf.Max(14, bodyFontSize - 2)
        };
        ApplyTextColor(statLabelStyle, MutedTextColor);

        statValueStyle = new GUIStyle(bodyStyle)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = bodyFontSize + 6,
            fontStyle = FontStyle.Bold
        };
        ApplyTextColor(statValueStyle, StatValueColor);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = buttonFontSize
        };
        buttonStyle.padding = new RectOffset(16, 16, 10, 10);
        ApplyTextColor(buttonStyle, MenuTextColor);
        SetButtonBackgrounds(buttonStyle, ButtonTint, ButtonHoverTint, ButtonActiveTint);

        selectedButtonStyle = new GUIStyle(buttonStyle);
        selectedButtonStyle.fontStyle = FontStyle.Bold;
        ApplyTextColor(selectedButtonStyle, SelectedButtonTextColor);
        SetButtonBackgrounds(selectedButtonStyle, SelectedButtonTint, SelectedButtonHoverTint, SelectedButtonActiveTint);

        hudStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = hudFontSize,
            fontStyle = FontStyle.Bold
        };
        ApplyTextColor(hudStyle, MenuTextColor);

        hudShadowStyle = new GUIStyle(hudStyle);
        ApplyTextColor(hudShadowStyle, ShadowTextColor);
    }

    private static void SetGameplayObjectsEnabled(bool enabled)
    {
        foreach (Move mover in FindObjectsByType<Move>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            mover.enabled = enabled;
        }

        foreach (SectionTrigger sectionTrigger in FindObjectsByType<SectionTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            sectionTrigger.enabled = enabled;
        }

        foreach (RunnerJumpController jumpController in FindObjectsByType<RunnerJumpController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            jumpController.enabled = enabled;
        }
    }

    private void ApplyState()
    {
        bool isPlaying = state == GameState.Playing;
        Time.timeScale = isPlaying ? 1f : 0f;
        SetGameplayObjectsEnabled(isPlaying);
    }

    private void UpdateCursorState()
    {
        if (isXrActive || state == GameState.Playing)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            return;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private bool DrawMenuButton(Rect buttonRect, string label)
    {
        return DrawMenuButton(buttonRect, label, buttonStyle);
    }

    private bool DrawMenuButton(Rect buttonRect, string label, GUIStyle style)
    {
        bool clicked = GUI.Button(buttonRect, label, style);

        Event currentEvent = Event.current;
        if (currentEvent != null
            && currentEvent.type == EventType.MouseDown
            && buttonRect.Contains(currentEvent.mousePosition))
        {
            clicked = true;
            currentEvent.Use();
        }

        return clicked;
    }

    private Rect GetMenuRect()
    {
        float width = Mathf.Min(menuWidth, Screen.width - 40f);
        float height = Mathf.Min(menuHeight, Screen.height - 40f);
        return new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);
    }

    private static Rect GetMenuContentRect(Rect panelRect)
    {
        return new Rect(panelRect.x + 24f, panelRect.y, panelRect.width - 48f, panelRect.height);
    }

    private int CalculateScore(float runTime)
    {
        return Mathf.FloorToInt(runTime * baseScorePerSecond * GetActiveProfile().ScoreMultiplier);
    }

    private static float EvaluateWorldSpeed(float runTime, DifficultyProfile profile)
    {
        float baseSpeed = Mathf.Lerp(
            profile.StartWorldSpeed,
            profile.SoftCapWorldSpeed,
            EvaluateSoftCapProgress(runTime, profile));

        return baseSpeed + EvaluateOverflowGrowth(runTime, profile);
    }

    private static float EvaluateSoftCapProgress(float runTime, DifficultyProfile profile)
    {
        float clampedRunTime = Mathf.Max(0f, runTime);
        float baseDuration = Mathf.Max(0.1f, profile.SpeedRampDuration);

        switch (profile.SpeedCurve)
        {
            case SpeedCurveType.SmoothStep:
            {
                float normalizedTime = Mathf.Clamp01(clampedRunTime / baseDuration);
                return normalizedTime * normalizedTime * (3f - (2f * normalizedTime));
            }

            case SpeedCurveType.LateBoost:
            {
                float earlyProgress = 1f - Mathf.Exp(-clampedRunTime / baseDuration);
                float lateBoostDuration = Mathf.Max(0.1f, profile.LateBoostDuration);
                float lateProgress = Mathf.Clamp01((clampedRunTime - profile.LateBoostStartTime) / lateBoostDuration);
                return Mathf.Clamp01(earlyProgress + ((1f - earlyProgress) * lateProgress));
            }

            default:
                return 1f - Mathf.Exp(-clampedRunTime / baseDuration);
        }
    }

    private static float EvaluateOverflowGrowth(float runTime, DifficultyProfile profile)
    {
        float overflowTime = Mathf.Max(0f, runTime - profile.OverflowStartTime);
        if (overflowTime <= 0f || profile.OverflowGrowthRate <= 0f || profile.OverflowGrowthScale <= 0f)
        {
            return 0f;
        }

        return Mathf.Log(1f + (overflowTime * profile.OverflowGrowthRate)) * profile.OverflowGrowthScale;
    }

    private static string FormatScore(int score)
    {
        return score.ToString();
    }

    private void RefreshXrState()
    {
        bool xrNowActive = IsXrDisplayRunning();
        isXrActive = xrNowActive;
        xrSessionDetected |= xrNowActive;

        if (!isXrActive)
        {
            xrSetupApplied = false;
            SetPlayerVisualsEnabled(true);
            if (xrMenuRoot != null)
            {
                xrMenuRoot.SetActive(false);
            }
            return;
        }

        ResolveXrReferences();
        if (!xrSetupApplied)
        {
            xrSetupApplied = ApplyXrSetup();
        }
    }

    private bool ApplyXrSetup()
    {
        if (playerRoot == null || xrRig == null)
        {
            return false;
        }

        xrRig.SetParent(null, true);
        xrRig.localScale = Vector3.one;
        xrRig.position = playerRoot.position + (Vector3.up * xrHeightOffset);
        xrRig.rotation = playerRoot.rotation;

        Transform cameraOffset = xrHead != null ? xrHead.parent : null;
        if (cameraOffset != null)
        {
            cameraOffset.localPosition = Vector3.zero;
            cameraOffset.localRotation = Quaternion.identity;
            cameraOffset.localScale = Vector3.one;
        }

        if (xrHead != null)
        {
            xrHead.localPosition = Vector3.zero;
            xrHead.localRotation = Quaternion.identity;
            xrHead.localScale = Vector3.one;
        }

        CachePlayerVisuals();
        SetPlayerVisualsEnabled(false);
        if (!useWorldSpaceMenuPresentation)
        {
            EnsureXrMenu();
            UpdateXrMenuState();
        }
        return true;
    }

    private void ResolveXrReferences()
    {
        if (playerRoot == null)
        {
            GameObject playerObject = GameObject.Find(xrPlayerRootName);
            if (playerObject == null)
            {
                RunnerLateralMovement runner = FindFirstObjectByType<RunnerLateralMovement>();
                if (runner != null)
                {
                    playerObject = runner.gameObject;
                }
            }

            if (playerObject != null)
            {
                playerRoot = playerObject.transform;
            }
        }

        if (xrRig == null)
        {
            GameObject xrRigObject = GameObject.Find(xrRigName);
            if (xrRigObject != null)
            {
                xrRig = xrRigObject.transform;
            }
        }

        if (xrHead == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                xrHead = mainCamera.transform;
            }
        }
    }

    private bool IsXrDisplayRunning()
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

    private void AlignXrRigToPlayer()
    {
        if (playerRoot == null || xrRig == null)
        {
            return;
        }

        xrRig.position = playerRoot.position + (Vector3.up * xrHeightOffset);
        xrRig.rotation = playerRoot.rotation;
    }

    private void CachePlayerVisuals()
    {
        if (playerRenderers == null && playerRoot != null)
        {
            playerRenderers = playerRoot.GetComponentsInChildren<Renderer>(true);
        }

        if (playerAnimators == null && playerRoot != null)
        {
            playerAnimators = playerRoot.GetComponentsInChildren<Animator>(true);
        }
    }

    private void SetPlayerVisualsEnabled(bool enabled)
    {
        if (enabled == !xrVisualsHidden)
        {
            return;
        }

        CachePlayerVisuals();

        if (playerRenderers != null)
        {
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                if (playerRenderers[i] != null)
                {
                    playerRenderers[i].enabled = enabled;
                }
            }
        }

        if (playerAnimators != null)
        {
            for (int i = 0; i < playerAnimators.Length; i++)
            {
                if (playerAnimators[i] != null)
                {
                    playerAnimators[i].enabled = enabled;
                }
            }
        }

        xrVisualsHidden = !enabled;
    }

    private void EnsureMenuConfirmAction()
    {
        if (menuConfirmAction != null)
        {
            return;
        }

        menuConfirmAction = new InputAction("MenuConfirm", InputActionType.Button);
        menuConfirmAction.AddBinding("<XRController>{RightHand}/primaryButton");
        menuConfirmAction.AddBinding("<XRController>{LeftHand}/primaryButton");
        menuConfirmAction.AddBinding("<XRController>{RightHand}/triggerButton");
        menuConfirmAction.AddBinding("<Keyboard>/space");
        menuConfirmAction.AddBinding("<Keyboard>/enter");
        menuConfirmAction.AddBinding("<Gamepad>/buttonSouth");
        menuConfirmAction.Enable();
    }

    private void DisableMenuConfirmAction()
    {
        if (menuConfirmAction == null)
        {
            return;
        }

        menuConfirmAction.Disable();
        menuConfirmAction.Dispose();
        menuConfirmAction = null;
    }

    private void UpdateMenuInput()
    {
        if (useWorldSpaceMenuPresentation)
        {
            return;
        }

        if (menuConfirmAction == null || !menuConfirmAction.WasPressedThisFrame())
        {
            return;
        }

        if (state == GameState.PreGame)
        {
            StartRun();
            return;
        }

        if (state == GameState.GameOver)
        {
            ReturnToStartScreen();
        }
    }

    private void EnsureXrMenu()
    {
        if (!enableLegacyMenus)
        {
            if (xrMenuRoot != null)
            {
                xrMenuRoot.SetActive(false);
            }
            return;
        }

        if (FindFirstObjectByType<VRMenuUI>() != null)
        {
            if (xrMenuRoot != null)
            {
                xrMenuRoot.SetActive(false);
            }
            return;
        }

        if (xrMenuRoot != null)
        {
            return;
        }

        xrMenuRoot = new GameObject("XR Start Menu");

        GameObject panelObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panelObject.name = "Panel";
        panelObject.transform.SetParent(xrMenuRoot.transform, false);
        panelObject.transform.localScale = new Vector3(1.25f, 0.8f, 1f);
        panelObject.transform.localPosition = new Vector3(0f, 0f, 0.02f);

        Collider panelCollider = panelObject.GetComponent<Collider>();
        if (panelCollider != null)
        {
            Destroy(panelCollider);
        }

        Renderer panelRenderer = panelObject.GetComponent<Renderer>();
        if (panelRenderer != null)
        {
            panelRenderer.material.color = new Color(0.08f, 0.08f, 0.1f, 1f);
        }

        xrMenuTitle = CreateMenuText("Title", new Vector3(0f, 0.2f, 0f), 96, 0.012f, TextAnchor.MiddleCenter, FontStyle.Bold);
        xrMenuStatus = CreateMenuText("Status", new Vector3(0f, 0.03f, 0f), 72, 0.01f, TextAnchor.MiddleCenter, FontStyle.Normal);
        xrMenuAction = CreateMenuText("Action", new Vector3(0f, -0.18f, 0f), 72, 0.01f, TextAnchor.MiddleCenter, FontStyle.Bold);

        xrMenuTitle.transform.SetParent(xrMenuRoot.transform, false);
        xrMenuStatus.transform.SetParent(xrMenuRoot.transform, false);
        xrMenuAction.transform.SetParent(xrMenuRoot.transform, false);
    }

    private TextMesh CreateMenuText(
        string name,
        Vector3 localPosition,
        int fontSize,
        float characterSize,
        TextAnchor anchor,
        FontStyle fontStyle)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.localPosition = localPosition;

        TextMesh textMesh = textObject.AddComponent<TextMesh>();
        textMesh.anchor = anchor;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = fontSize;
        textMesh.characterSize = characterSize;
        textMesh.fontStyle = fontStyle;
        textMesh.color = Color.white;
        return textMesh;
    }

    private void UpdateXrMenuState()
    {
        if (!enableLegacyMenus)
        {
            if (xrMenuRoot != null && xrMenuRoot.activeSelf)
            {
                xrMenuRoot.SetActive(false);
            }
            return;
        }

        if (FindFirstObjectByType<VRMenuUI>() != null)
        {
            if (xrMenuRoot != null && xrMenuRoot.activeSelf)
            {
                xrMenuRoot.SetActive(false);
            }
            return;
        }

        if (useWorldSpaceMenuPresentation)
        {
            if (xrMenuRoot != null && xrMenuRoot.activeSelf)
            {
                xrMenuRoot.SetActive(false);
            }
            return;
        }

        if (!isXrActive || xrMenuRoot == null)
        {
            return;
        }

        bool showMenu = state != GameState.Playing;
        xrMenuRoot.SetActive(showMenu);
        if (!showMenu)
        {
            return;
        }

        if (state == GameState.PreGame)
        {
            xrMenuTitle.text = gameTitle;
            xrMenuStatus.text = $"High Score: {FormatScore(GetHighScore(CurrentMode))}";
            xrMenuAction.text = "Press A / Trigger / Space to Play";
            return;
        }

        xrMenuTitle.text = "Run Failed";
        xrMenuStatus.text = $"Score: {FormatScore(CurrentScore)}   Best: {FormatScore(GetHighScore(activeMode))}";
        xrMenuAction.text = "Press A / Trigger / Space to Continue";
    }

    private void UpdateXrMenuPose()
    {
        if (!enableLegacyMenus)
        {
            return;
        }

        if (useWorldSpaceMenuPresentation)
        {
            return;
        }

        if (!isXrActive || xrMenuRoot == null || !xrMenuRoot.activeSelf || xrHead == null)
        {
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(xrHead.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();

        xrMenuRoot.transform.position = xrHead.position + (forward * xrMenuDistance) + (Vector3.up * xrMenuVerticalOffset);
        xrMenuRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    private static string FormatScoreMultiplier(float multiplier)
    {
        return $"Score x{multiplier:0.##}";
    }

    private static string FormatSpeedStat(float speed)
    {
        return speed > 0f ? speed.ToString("0.0") : "--";
    }

    private static string GetHighScoreKey(GameMode mode)
    {
        return HighScoreKeyPrefix + mode;
    }

    private static string GetPeakSpeedKey(GameMode mode)
    {
        return PeakSpeedKeyPrefix + mode;
    }

    private static int GetHighScore(GameMode mode)
    {
        return PlayerPrefs.GetInt(GetHighScoreKey(mode), 0);
    }

    private static float GetBestPeakSpeed(GameMode mode)
    {
        return PlayerPrefs.GetFloat(GetPeakSpeedKey(mode), 0f);
    }

    private static void SaveHighScore(GameMode mode, int score)
    {
        PlayerPrefs.SetInt(GetHighScoreKey(mode), score);
        PlayerPrefs.Save();
    }

    private static void SaveBestPeakSpeed(GameMode mode, float speed)
    {
        PlayerPrefs.SetFloat(GetPeakSpeedKey(mode), speed);
        PlayerPrefs.Save();
    }

    private static GameMode LoadSelectedMode()
    {
        int storedMode = PlayerPrefs.GetInt(SelectedModeKey, (int)GameMode.Medium);
        return System.Enum.IsDefined(typeof(GameMode), storedMode)
            ? (GameMode)storedMode
            : GameMode.Medium;
    }

    private static void SaveSelectedMode(GameMode mode)
    {
        PlayerPrefs.SetInt(SelectedModeKey, (int)mode);
        PlayerPrefs.Save();
    }

    private DifficultyProfile GetActiveProfile()
    {
        return GetProfile(CurrentMode);
    }

    private static DifficultyProfile GetProfile(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Easy:
                return new DifficultyProfile(
                    "Easy",
                    SpeedCurveType.SmoothStep,
                    4f,
                    14f,
                    45f,
                    0f,
                    0f,
                    45f,
                    0.035f,
                    1.25f,
                    1f,
                    5.25f,
                    4.8f,
                    2);

            case GameMode.Hard:
                return new DifficultyProfile(
                    "Hard",
                    SpeedCurveType.LateBoost,
                    5.2f,
                    22f,
                    16f,
                    20f,
                    14f,
                    34f,
                    0.07f,
                    2.1f,
                    1.75f,
                    4.8f,
                    4.05f,
                    0);

            default:
                return new DifficultyProfile(
                    "Medium",
                    SpeedCurveType.Exponential,
                    4.7f,
                    19f,
                    18f,
                    0f,
                    0f,
                    26f,
                    0.06f,
                    1.65f,
                    1.35f,
                    5f,
                    4.3f,
                    1);
        }
    }

    private static void ApplyTextColor(GUIStyle style, Color color)
    {
        style.normal.textColor = color;
        style.hover.textColor = color;
        style.active.textColor = color;
        style.focused.textColor = color;
    }

    private static void SetButtonBackgrounds(GUIStyle style, Color normal, Color hover, Color active)
    {
        style.normal.background = CreateSolidTexture(normal);
        style.hover.background = CreateSolidTexture(hover);
        style.active.background = CreateSolidTexture(active);
        style.focused.background = style.normal.background;
    }

    private void DrawHudLine(float x, float y, string text)
    {
        Rect rect = new Rect(x, y, 300f, 32f);
        DrawLabelWithShadow(rect, text, hudStyle, hudShadowStyle, 2f);
    }

    private static void DrawLabelWithShadow(Rect rect, string text, GUIStyle style, GUIStyle shadowStyle, float shadowOffset = 3f)
    {
        GUI.Label(new Rect(rect.x + shadowOffset, rect.y + shadowOffset, rect.width, rect.height), text, shadowStyle);
        GUI.Label(rect, text, style);
    }

    private void DrawStatBlock(Rect rect, string label, string value)
    {
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 20f), label, statLabelStyle);
        GUI.Label(new Rect(rect.x, rect.y + 18f, rect.width, 30f), value, statValueStyle);
    }

    private static void DrawPanel(Rect rect, Color fillColor, Color borderColor)
    {
        DrawFilledRect(rect, borderColor);
        DrawFilledRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), fillColor);
    }

    private static void DrawFilledRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private static Rect InsetRect(Rect rect, float horizontalInset, float verticalInset)
    {
        return new Rect(
            rect.x + horizontalInset,
            rect.y + verticalInset,
            rect.width - (horizontalInset * 2f),
            rect.height - (verticalInset * 2f));
    }

    private static Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
