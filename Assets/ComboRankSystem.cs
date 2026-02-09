using UnityEngine;

/// <summary>
/// Samira-like combo rank system:
/// E -> D -> C -> B -> A -> S -> SxN
/// Grants attack-speed bonus based on rank and extra S stacks.
/// </summary>
public class ComboRankSystem : MonoBehaviour
{
    public static ComboRankSystem Instance { get; private set; }

    [Header("Combo Timing")]
    [Min(0.2f)] public float comboExpireTime = 3.0f;
    [Min(0.1f)] public float decayStepInterval = 0.7f;

    [Header("Attack Speed Bonus")]
    [Tooltip("Attack speed multiplier by rank index: E,D,C,B,A,S.")]
    public float[] attackSpeedByRank = new float[] { 1.0f, 1.06f, 1.12f, 1.22f, 1.34f, 1.5f };
    [Tooltip("Extra attack speed per additional S kill.")]
    [Min(0f)] public float extraSBonusPerKill = 0.04f;
    [Tooltip("Max additional attack speed from S stacks.")]
    [Min(0f)] public float extraSBonusCap = 0.6f;

    [Header("UI")]
    public bool showOnGUI = true;
    [Tooltip("Top-left anchored screen position.")]
    public Vector2 rankUIPosition = new Vector2(28f, 24f);
    public float rankUIScale = 1f;
    public Color rankColor = new Color(1f, 0.84f, 0.25f, 1f);
    [Min(12)] public int baseRankFontSize = 78;
    [Min(8)] public int sMinFontSize = 20;
    [Min(1)] public int sFontStep = 6;
    [Min(1)] public int maxExplicitS = 8;
    [Header("UI FX")]
    [Range(0f, 12f)] public float uiJitterPixels = 1.8f;
    [Range(0f, 20f)] public float uiJitterSpeed = 7f;
    [Range(0f, 1f)] public float uiFlickerAmplitude = 0.12f;
    [Range(0f, 20f)] public float uiFlickerSpeed = 9f;
    [Min(0f)] public float uiSpeedGainPerTier = 0.1f;
    [Min(0f)] public float uiSpeedGainPerExtraS = 0.06f;

    private static readonly string[] RankLetters = { "E", "D", "C", "B", "A", "S" };

    private int _rankIndex = -1; // -1 means no active combo.
    private int _extraSCount;
    private int _totalComboKills;
    private float _comboTimer;
    private float _decayTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("ComboRankSystem");
        DontDestroyOnLoad(go);
        go.AddComponent<ComboRankSystem>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!HasActiveCombo()) return;

        _comboTimer -= Time.deltaTime;
        if (_comboTimer > 0f) return;

        _decayTimer -= Time.deltaTime;
        if (_decayTimer > 0f) return;

        DecayOneStep();
        _decayTimer = decayStepInterval;
    }

    public static void RegisterKillGlobal()
    {
        if (Instance == null) return;
        Instance.RegisterKill();
    }

    public static float GetAttackSpeedMultiplierGlobal()
    {
        if (Instance == null) return 1f;
        return Instance.GetAttackSpeedMultiplier();
    }

    public static void ResetRunGlobal()
    {
        if (Instance == null) return;
        Instance.ResetRun();
    }

    private void RegisterKill()
    {
        if (_rankIndex < RankLetters.Length - 1)
        {
            _rankIndex++;
        }
        else
        {
            _extraSCount++;
        }

        _totalComboKills++;
        _comboTimer = comboExpireTime;
        _decayTimer = decayStepInterval;
    }

    private void ResetRun()
    {
        _rankIndex = -1;
        _extraSCount = 0;
        _totalComboKills = 0;
        _comboTimer = 0f;
        _decayTimer = 0f;
    }

    private void DecayOneStep()
    {
        if (_extraSCount > 0)
        {
            _extraSCount--;
            return;
        }

        _rankIndex--;
        if (_rankIndex < 0)
        {
            ResetRun();
        }
    }

    private bool HasActiveCombo()
    {
        return _rankIndex >= 0;
    }

    private float GetAttackSpeedMultiplier()
    {
        if (_rankIndex < 0) return 1f;

        int idx = Mathf.Clamp(_rankIndex, 0, attackSpeedByRank.Length - 1);
        float mult = attackSpeedByRank[idx];
        if (_rankIndex >= RankLetters.Length - 1 && _extraSCount > 0)
        {
            mult += Mathf.Min(extraSBonusCap, _extraSCount * extraSBonusPerKill);
        }
        return Mathf.Max(0.1f, mult);
    }

    private string GetRankText()
    {
        if (_rankIndex < 0) return "-";
        string letter = RankLetters[Mathf.Clamp(_rankIndex, 0, RankLetters.Length - 1)];
        return letter;
    }

    private void OnGUI()
    {
        if (!showOnGUI) return;
        if (_rankIndex < 0) return;

        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * Mathf.Max(0.1f, rankUIScale));

        if (_rankIndex < RankLetters.Length - 1)
        {
            DrawSingleRank(GetRankText());
            return;
        }

        DrawSChain();
    }

    private void DrawSingleRank(string txt)
    {
        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontStyle = FontStyle.Bold;
        st.fontSize = Mathf.Max(12, baseRankFontSize);
        st.normal.textColor = GetFxColor();
        st.alignment = TextAnchor.UpperLeft;

        Vector2 jitter = GetFxJitter();
        float x = rankUIPosition.x + jitter.x;
        float y = rankUIPosition.y + jitter.y;
        Vector2 size = st.CalcSize(new GUIContent(txt));
        GUI.Label(new Rect(x, y, size.x + 8f, size.y + 8f), txt, st);
    }

    private void DrawSChain()
    {
        int count = 1 + Mathf.Max(0, _extraSCount);
        if (count > Mathf.Max(1, maxExplicitS))
        {
            DrawSCompact(count);
            return;
        }

        int baseSize = Mathf.Max(12, baseRankFontSize);
        int minSize = Mathf.Clamp(sMinFontSize, 8, baseSize);

        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontStyle = FontStyle.Bold;
        st.normal.textColor = GetFxColor();
        st.alignment = TextAnchor.UpperLeft;

        float[] widths = new float[count];
        int[] sizes = new int[count];
        float totalW = 0f;
        for (int i = 0; i < count; i++)
        {
            int fs = Mathf.Max(minSize, baseSize - sFontStep * i);
            sizes[i] = fs;
            st.fontSize = fs;
            widths[i] = st.CalcSize(new GUIContent("S")).x;
            totalW += widths[i];
            if (i < count - 1) totalW += 2f;
        }

        Vector2 jitter = GetFxJitter();
        float x = rankUIPosition.x + jitter.x;
        float y = rankUIPosition.y + jitter.y;
        for (int i = 0; i < count; i++)
        {
            st.fontSize = sizes[i];
            GUI.Label(new Rect(x, y, widths[i] + 4f, sizes[i] + 8f), "S", st);
            x += widths[i] + 2f;
        }
    }

    private void DrawSCompact(int count)
    {
        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontStyle = FontStyle.Bold;
        st.fontSize = Mathf.Max(12, baseRankFontSize);
        st.normal.textColor = GetFxColor();
        st.alignment = TextAnchor.UpperLeft;

        string txt = $"Sx{count}";
        Vector2 jitter = GetFxJitter();
        Vector2 size = st.CalcSize(new GUIContent(txt));
        GUI.Label(new Rect(rankUIPosition.x + jitter.x, rankUIPosition.y + jitter.y, size.x + 8f, size.y + 8f), txt, st);
    }

    private Vector2 GetFxJitter()
    {
        if (uiJitterPixels <= 0.001f) return Vector2.zero;
        float speedMul = GetUiSpeedMultiplier();
        float t = Time.unscaledTime * Mathf.Max(0.01f, uiJitterSpeed) * speedMul;
        float nx = Mathf.PerlinNoise(13.17f, t) * 2f - 1f;
        float ny = Mathf.PerlinNoise(91.37f, t + 13.1f) * 2f - 1f;
        return new Vector2(nx, ny) * uiJitterPixels;
    }

    private Color GetFxColor()
    {
        Color c = rankColor;
        float amp = Mathf.Clamp01(uiFlickerAmplitude);
        if (amp <= 0.001f) return c;
        float speedMul = GetUiSpeedMultiplier();
        float t = Time.unscaledTime * Mathf.Max(0.01f, uiFlickerSpeed) * speedMul;
        float pulse = (Mathf.Sin(t) + 1f) * 0.5f; // 0..1
        c.a *= Mathf.Lerp(1f - amp, 1f, pulse);
        return c;
    }

    private float GetUiSpeedMultiplier()
    {
        if (_rankIndex < 0) return 1f;
        int tier = Mathf.Clamp(_rankIndex, 0, RankLetters.Length - 1);
        float mul = 1f + tier * Mathf.Max(0f, uiSpeedGainPerTier);
        if (_extraSCount > 0)
        {
            mul += _extraSCount * Mathf.Max(0f, uiSpeedGainPerExtraS);
        }
        return Mathf.Max(0.1f, mul);
    }
}
