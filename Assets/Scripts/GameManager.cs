using UnityEngine;
using UnityEngine.SceneManagement;

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

    private GameState state = GameState.PreGame;
    private float currentRunTime;
    private int highScore;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle hudStyle;

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

    private void Update()
    {
        UpdateCursorState();

        if (state == GameState.Playing)
        {
            currentRunTime += Time.deltaTime;
        }
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

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnGUI()
    {
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
        if (state == GameState.Playing)
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

}
