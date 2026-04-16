using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class GameManager : MonoBehaviour
{
    private const string HighScoreKey = "HighScoreValue";

    private enum GameState
    {
        PreGame,
        Playing,
        GameOver
    }

    public static GameManager Instance { get; private set; }

    public bool IsPlaying => state == GameState.Playing;
    public int CurrentScore => CalculateScore(currentRunTime);
    public int HighScore => highScore;

    [Header("Menu")]
    [SerializeField] private string gameTitle = "VR Runner Road";
    [SerializeField, Min(320f)] private float menuWidth = 420f;
    [SerializeField, Min(220f)] private float menuHeight = 250f;
    [SerializeField, Min(20)] private int titleFontSize = 30;
    [SerializeField, Min(16)] private int bodyFontSize = 20;
    [SerializeField, Min(16)] private int buttonFontSize = 20;
    [SerializeField, Min(16)] private int hudFontSize = 22;
    [SerializeField, Min(1f)] private float scorePerSecond = 10f;

    [Header("XR")]
    [SerializeField] private string xrPlayerRootName = "PlayerArmature";
    [SerializeField] private string xrRigName = "XRRig";
    [SerializeField] private float xrHeightOffset = 0.1f;
    [SerializeField, Min(0.5f)] private float xrMenuDistance = 2f;
    [SerializeField] private float xrMenuVerticalOffset = -0.1f;

    private GameState state = GameState.PreGame;
    private float currentRunTime;
    private int highScore;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle hudStyle;
    private readonly System.Collections.Generic.List<XRDisplaySubsystem> xrDisplays = new();
    private InputAction menuConfirmAction;
    private bool isXrActive;
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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
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
        currentRunTime = 0f;
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
        if (finalScore > highScore)
        {
            highScore = finalScore;
            PlayerPrefs.SetInt(HighScoreKey, highScore);
            PlayerPrefs.Save();
        }

        ApplyState();
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
        if (isXrActive)
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
        GUI.Box(panelRect, GUIContent.none);

        float contentX = panelRect.x + 20f;
        float contentWidth = panelRect.width - 40f;

        GUI.Label(new Rect(contentX, panelRect.y + 20f, contentWidth, 36f), gameTitle, titleStyle);
        GUI.Label(
            new Rect(contentX, panelRect.y + 78f, contentWidth, 28f),
            $"High Score: {FormatScore(highScore)}",
            bodyStyle);
        GUI.Label(new Rect(contentX, panelRect.y + 114f, contentWidth, 28f), "Click Play to begin.", bodyStyle);

        Rect buttonRect = new Rect(contentX, panelRect.y + panelRect.height - 70f, contentWidth, 42f);
        if (DrawMenuButton(buttonRect, "Play"))
        {
            StartRun();
        }
    }

    private void DrawFailureMenu()
    {
        Rect panelRect = GetMenuRect();
        GUI.Box(panelRect, GUIContent.none);

        float contentX = panelRect.x + 20f;
        float contentWidth = panelRect.width - 40f;

        GUI.Label(new Rect(contentX, panelRect.y + 20f, contentWidth, 36f), "Run Failed", titleStyle);
        GUI.Label(
            new Rect(contentX, panelRect.y + 78f, contentWidth, 28f),
            $"Score: {FormatScore(CurrentScore)}",
            bodyStyle);
        GUI.Label(
            new Rect(contentX, panelRect.y + 114f, contentWidth, 28f),
            $"High Score: {FormatScore(highScore)}",
            bodyStyle);

        Rect buttonRect = new Rect(contentX, panelRect.y + panelRect.height - 70f, contentWidth, 42f);
        if (DrawMenuButton(buttonRect, "Continue"))
        {
            ReturnToStartScreen();
        }
    }

    private void DrawScoreHud()
    {
        GUI.Label(new Rect(20f, 20f, 240f, 36f), $"Score: {FormatScore(CurrentScore)}", hudStyle);
        GUI.Label(new Rect(20f, 50f, 240f, 36f), $"Best: {FormatScore(highScore)}", hudStyle);
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

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = bodyFontSize
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = buttonFontSize
        };

        hudStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = hudFontSize,
            fontStyle = FontStyle.Bold
        };
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
        bool clicked = GUI.Button(buttonRect, label, buttonStyle);

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

    private int CalculateScore(float runTime)
    {
        return Mathf.FloorToInt(runTime * scorePerSecond);
    }

    private static string FormatScore(int score)
    {
        return score.ToString();
    }

    private void RefreshXrState()
    {
        bool xrNowActive = IsXrDisplayRunning();
        isXrActive = xrNowActive;

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
        EnsureXrMenu();
        UpdateXrMenuState();
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
            xrMenuStatus.text = $"High Score: {FormatScore(highScore)}";
            xrMenuAction.text = "Press A / Trigger / Space to Play";
            return;
        }

        xrMenuTitle.text = "Run Failed";
        xrMenuStatus.text = $"Score: {FormatScore(CurrentScore)}   Best: {FormatScore(highScore)}";
        xrMenuAction.text = "Press A / Trigger / Space to Continue";
    }

    private void UpdateXrMenuPose()
    {
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

}
