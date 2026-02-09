using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuOverlay : MonoBehaviour
{
    [Header("Menu")]
    public bool showOnStart = true;
    public bool pauseGameWhileMenuOpen = false;
    public string gameTitle = "ABYSS RUN";
    [TextArea] public string subtitle = "PRESS START";

    [Header("Start Action")]
    [Tooltip("If empty, menu closes and continues current scene.")]
    public string sceneToLoadOnStart = "";
    public bool allowKeyboardStart = true;
    public KeyCode startKeyPrimary = KeyCode.Space;
    public KeyCode startKeySecondary = KeyCode.Return;

    [Header("Player Control Gate")]
    public bool gatePlayerControl = true;
    public string playerTag = "Player";
    public Behaviour[] extraControlBehaviours;
    public bool freezePlayerPhysicsWhileMenuOpen = false;
    public bool autoDodgeWhileMenuOpen = true;

    [Header("Visual")]
    public Color dimBackground = new Color(0f, 0.03f, 0.08f, 0.55f);
    public Color panelColor = new Color(0.03f, 0.09f, 0.16f, 0.85f);
    public Color primaryButtonColor = new Color(0.1f, 0.55f, 0.85f, 1f);
    public Color secondaryButtonColor = new Color(0.22f, 0.22f, 0.28f, 1f);
    public Color textColor = new Color(0.88f, 0.96f, 1f, 1f);
    public float hintBlinkSpeed = 2.2f;

    [Header("Fallback")]
    public bool useOnGUIFallback = true;

    private GameObject _canvasGO;
    private Text _hintText;
    private Text _scoreText;
    private bool _menuOpen;
    private bool _bootRecoverTried;
    private bool _controlGateActive;
    private bool _hasRunScore;
    private int _lastRunScore;

    public bool IsMenuOpen => _menuOpen;

    private readonly List<Behaviour> _gatedBehaviours = new List<Behaviour>();
    private readonly List<Rigidbody> _gatedRigidbodies = new List<Rigidbody>();
    private readonly List<bool> _rbKinematicBackup = new List<bool>();
    private readonly List<Vector3> _rbVelBackup = new List<Vector3>();
    private readonly List<Vector3> _rbAngBackup = new List<Vector3>();
    private MenuAutoDodge _menuAutoDodge;

    private static readonly HashSet<string> ControlTypeNames = new HashSet<string>
    {
        "PlayerUnderwaterController",
        "playercon",
        "PlayerCon1",
        "AutoShooter"
    };

    private void Start()
    {
        if (autoDodgeWhileMenuOpen)
        {
            // Auto-dodge needs physics movement; freezing rigidbody here would block control restore.
            freezePlayerPhysicsWhileMenuOpen = false;
        }

        ResetMenuState();
        if (!showOnStart) return;
        OpenMenu();
    }

    private void Update()
    {
        if (!_bootRecoverTried && showOnStart)
        {
            _bootRecoverTried = true;
            if (_canvasGO == null && !_menuOpen)
                OpenMenu();
        }

        if (_menuOpen && _canvasGO == null && !useOnGUIFallback)
        {
            _menuOpen = false;
            OpenMenu();
        }

        if (!_menuOpen) return;

        EnsureRuntimeMenuBindings();

        if (_hintText != null)
        {
            Color c = _hintText.color;
            c.a = Mathf.Lerp(0.25f, 1f, (Mathf.Sin(Time.unscaledTime * hintBlinkSpeed) + 1f) * 0.5f);
            _hintText.color = c;
        }

        if (allowKeyboardStart && (Input.GetKeyDown(startKeyPrimary) || Input.GetKeyDown(startKeySecondary)))
            StartGame();
    }

    [ContextMenu("Open Menu")]
    public void OpenMenu()
    {
        if (_menuOpen && _canvasGO != null) return;
        if (_canvasGO != null) Destroy(_canvasGO);
        _menuOpen = false;

        EnsureEventSystem();
        try
        {
            BuildUI();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[MainMenuOverlay] Canvas build failed, fallback OnGUI only. " + ex.Message);
            _canvasGO = null;
        }

        _menuOpen = true;
        if (pauseGameWhileMenuOpen) Time.timeScale = 0f;
        ApplyPlayerControlGate(false);
        Debug.Log("[MainMenuOverlay] Menu opened.");
    }

    public void StartGame()
    {
        // Always restore control first for single-scene arcade flow.
        CloseMenu();
        _hasRunScore = false;
        _lastRunScore = 0;
        CoinContainerDisplay.ResetGlobalScore();
        ComboRankSystem.ResetRunGlobal();

        if (!string.IsNullOrWhiteSpace(sceneToLoadOnStart))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneToLoadOnStart);
        }
    }

    public void SetLastRunScore(int score)
    {
        _hasRunScore = true;
        _lastRunScore = Mathf.Max(0, score);
        if (_scoreText != null)
        {
            _scoreText.text = $"Score: {_lastRunScore}";
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void CloseMenu()
    {
        if (!_menuOpen) return;
        _menuOpen = false;

        if (pauseGameWhileMenuOpen) Time.timeScale = 1f;
        ApplyPlayerControlGate(true);

        if (_canvasGO != null) Destroy(_canvasGO);
        _canvasGO = null;
    }

    private void ResetMenuState()
    {
        _menuOpen = false;
        _hintText = null;
        _scoreText = null;
        _bootRecoverTried = false;
        _controlGateActive = false;
        if (_canvasGO != null)
        {
            Destroy(_canvasGO);
            _canvasGO = null;
        }
    }

    private void ApplyPlayerControlGate(bool enableControl)
    {
        if (!gatePlayerControl) return;

        if (!enableControl)
        {
            if (_controlGateActive)
            {
                EnsureRuntimeMenuBindings();
                return;
            }

            _controlGateActive = true;

            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                MonoBehaviour[] all = player.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    MonoBehaviour mb = all[i];
                    if (mb == null) continue;
                    if (!ControlTypeNames.Contains(mb.GetType().Name)) continue;
                    if (autoDodgeWhileMenuOpen && mb is PlayerUnderwaterController uwCtrl)
                    {
                        uwCtrl.SetUseExternalInput(true);
                        continue;
                    }
                    if (!mb.enabled) continue;
                    mb.enabled = false;
                    _gatedBehaviours.Add(mb);
                }

                if (freezePlayerPhysicsWhileMenuOpen && !autoDodgeWhileMenuOpen)
                {
                    Rigidbody[] rbs = player.GetComponentsInChildren<Rigidbody>(true);
                    for (int i = 0; i < rbs.Length; i++)
                    {
                        Rigidbody rb = rbs[i];
                        if (rb == null) continue;
                        _gatedRigidbodies.Add(rb);
                        _rbKinematicBackup.Add(rb.isKinematic);
                        _rbVelBackup.Add(rb.linearVelocity);
                        _rbAngBackup.Add(rb.angularVelocity);
                        rb.isKinematic = true;
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }

                if (autoDodgeWhileMenuOpen)
                {
                    GameObject dodgeHost = ResolveControlHost(player);
                    _menuAutoDodge = dodgeHost.GetComponent<MenuAutoDodge>();
                    if (_menuAutoDodge == null)
                    {
                        _menuAutoDodge = dodgeHost.AddComponent<MenuAutoDodge>();
                    }
                    _menuAutoDodge.SetAutoDodgeEnabled(true);
                }
            }

            if (extraControlBehaviours != null)
            {
                for (int i = 0; i < extraControlBehaviours.Length; i++)
                {
                    Behaviour b = extraControlBehaviours[i];
                    if (b != null && b.enabled)
                    {
                        b.enabled = false;
                        _gatedBehaviours.Add(b);
                    }
                }
            }
        }
        else
        {
            if (!_controlGateActive) return;
            _controlGateActive = false;

            for (int i = 0; i < _gatedBehaviours.Count; i++)
            {
                if (_gatedBehaviours[i] != null) _gatedBehaviours[i].enabled = true;
            }
            _gatedBehaviours.Clear();

            for (int i = 0; i < _gatedRigidbodies.Count; i++)
            {
                Rigidbody rb = _gatedRigidbodies[i];
                if (rb == null) continue;
                rb.isKinematic = _rbKinematicBackup[i];
                rb.linearVelocity = _rbVelBackup[i];
                rb.angularVelocity = _rbAngBackup[i];
            }
            _gatedRigidbodies.Clear();
            _rbKinematicBackup.Clear();
            _rbVelBackup.Clear();
            _rbAngBackup.Clear();

            if (_menuAutoDodge != null)
            {
                _menuAutoDodge.SetAutoDodgeEnabled(false);
            }

            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                PlayerUnderwaterController[] uwCtrls = player.GetComponentsInChildren<PlayerUnderwaterController>(true);
                for (int i = 0; i < uwCtrls.Length; i++)
                {
                    if (uwCtrls[i] != null) uwCtrls[i].SetUseExternalInput(false);
                }

                GameObject controlHost = ResolveControlHost(player);
                if (controlHost != null)
                {
                    Rigidbody rb = controlHost.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = false;
                }
            }
        }
    }

    private void EnsureRuntimeMenuBindings()
    {
        if (!autoDodgeWhileMenuOpen) return;
        if (!_menuOpen) return;

        if (_menuAutoDodge != null)
        {
            _menuAutoDodge.SetAutoDodgeEnabled(true);
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null) return;

        GameObject dodgeHost = ResolveControlHost(player);
        _menuAutoDodge = dodgeHost.GetComponent<MenuAutoDodge>();
        if (_menuAutoDodge == null)
        {
            _menuAutoDodge = dodgeHost.AddComponent<MenuAutoDodge>();
        }
        _menuAutoDodge.SetAutoDodgeEnabled(true);
    }

    private GameObject ResolveControlHost(GameObject playerRoot)
    {
        if (playerRoot == null) return null;

        MonoBehaviour[] all = playerRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < all.Length; i++)
        {
            MonoBehaviour mb = all[i];
            if (mb == null) continue;
            if (!ControlTypeNames.Contains(mb.GetType().Name)) continue;
            return mb.gameObject;
        }

        return playerRoot;
    }

    private void BuildUI()
    {
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        _canvasGO = new GameObject("MainMenuCanvas");
        Canvas canvas = _canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        _canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _canvasGO.AddComponent<GraphicRaycaster>();

        GameObject dim = CreateUIObject("Dim", _canvasGO.transform);
        Image dimImg = dim.AddComponent<Image>();
        dimImg.color = dimBackground;
        StretchFull(dim.GetComponent<RectTransform>());

        GameObject panel = CreateUIObject("Panel", _canvasGO.transform);
        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = panelColor;
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(620f, 420f);
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;

        CreateText("Title", panel.transform, gameTitle, font, 64, TextAnchor.MiddleCenter, textColor, new Vector2(0f, 120f), new Vector2(560f, 100f));
        _hintText = CreateText("Hint", panel.transform, subtitle, font, 34, TextAnchor.MiddleCenter, new Color(0.78f, 0.92f, 1f, 1f), new Vector2(0f, 34f), new Vector2(540f, 70f));
        string scoreLabel = _hasRunScore ? $"Score: {_lastRunScore}" : "";
        _scoreText = CreateText("Score", panel.transform, scoreLabel, font, 24, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.45f, 1f), new Vector2(0f, -5f), new Vector2(540f, 40f));

        Button startBtn = CreateButton("StartButton", panel.transform, "Start", font, primaryButtonColor, new Vector2(0f, -40f), new Vector2(360f, 70f));
        startBtn.onClick.AddListener(StartGame);

        Button quitBtn = CreateButton("QuitButton", panel.transform, "Quit", font, secondaryButtonColor, new Vector2(0f, -128f), new Vector2(360f, 58f));
        quitBtn.onClick.AddListener(QuitGame);
    }

    private static Button CreateButton(string name, Transform parent, string label, Font font, Color bg, Vector2 pos, Vector2 size)
    {
        GameObject go = CreateUIObject(name, parent);
        Image img = go.AddComponent<Image>();
        img.color = bg;
        Button btn = go.AddComponent<Button>();

        ColorBlock cb = btn.colors;
        cb.normalColor = bg;
        cb.highlightedColor = Color.Lerp(bg, Color.white, 0.12f);
        cb.pressedColor = Color.Lerp(bg, Color.black, 0.15f);
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = new Color(bg.r, bg.g, bg.b, 0.45f);
        btn.colors = cb;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        CreateText(name + "_Text", go.transform, label, font, 30, TextAnchor.MiddleCenter, Color.white, Vector2.zero, size);
        return btn;
    }

    private static Text CreateText(string name, Transform parent, string content, Font font, int fontSize, TextAnchor anchor, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = CreateUIObject(name, parent);
        Text t = go.AddComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return t;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        int uiLayer = LayerMask.NameToLayer("UI");
        go.layer = uiLayer >= 0 ? uiLayer : 0;
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    private void OnGUI()
    {
        if (!useOnGUIFallback || !_menuOpen) return;

        Color old = GUI.color;
        float sw = Screen.width;
        float sh = Screen.height;

        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);

        float w = Mathf.Min(520f, sw * 0.78f);
        float h = Mathf.Min(300f, sh * 0.72f);
        float x = (sw - w) * 0.5f;
        float y = (sh - h) * 0.5f;

        GUI.color = new Color(0.05f, 0.1f, 0.18f, 0.95f);
        GUI.Box(new Rect(x, y, w, h), GUIContent.none);

        GUI.color = textColor;
        GUIStyle title = new GUIStyle(GUI.skin.label);
        title.alignment = TextAnchor.MiddleCenter;
        title.fontSize = 36;
        title.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(x + 20f, y + 24f, w - 40f, 52f), gameTitle, title);

        GUIStyle hint = new GUIStyle(GUI.skin.label);
        hint.alignment = TextAnchor.MiddleCenter;
        hint.fontSize = 20;
        Color hc = textColor;
        hc.a = Mathf.Lerp(0.25f, 1f, (Mathf.Sin(Time.unscaledTime * hintBlinkSpeed) + 1f) * 0.5f);
        hint.normal.textColor = hc;
        GUI.Label(new Rect(x + 20f, y + 82f, w - 40f, 36f), subtitle, hint);

        if (_hasRunScore)
        {
            GUIStyle score = new GUIStyle(GUI.skin.label);
            score.alignment = TextAnchor.MiddleCenter;
            score.fontSize = 20;
            score.fontStyle = FontStyle.Bold;
            score.normal.textColor = new Color(1f, 0.88f, 0.45f, 1f);
            GUI.Label(new Rect(x + 20f, y + 118f, w - 40f, 32f), $"Score: {_lastRunScore}", score);
        }

        GUIStyle btn = new GUIStyle(GUI.skin.button);
        btn.fontSize = 22;
        if (GUI.Button(new Rect(x + 70f, y + h - 128f, w - 140f, 46f), "Start", btn))
            StartGame();

        if (GUI.Button(new Rect(x + 70f, y + h - 72f, w - 140f, 40f), "Quit", btn))
            QuitGame();

        GUI.color = old;
    }
}
