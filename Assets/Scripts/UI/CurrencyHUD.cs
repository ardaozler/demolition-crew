#nullable enable
using UnityEngine;

/// <summary>
/// Displays team currency on screen. Uses OnGUI to match
/// the existing UI style (NetworkMenuUI, DebugPanel).
/// Auto-creates itself at runtime.
/// </summary>
public class CurrencyHUD : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        var go = new GameObject("CurrencyHUD");
        go.AddComponent<CurrencyHUD>();
        DontDestroyOnLoad(go);
    }

    private GUIStyle? _currencyStyle;
    private GUIStyle? _popStyle;
    private bool _stylesInitialized;

    private int _displayedCurrency;
    private int _lastAwardAmount;
    private float _popTimer;
    private const float PopDuration = 1.5f;

    private void OnEnable()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged += HandleCurrencyChanged;
    }

    private void OnDisable()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged -= HandleCurrencyChanged;
    }

    private void Update()
    {
        // Re-subscribe if CurrencyManager spawns after us
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCurrencyChanged -= HandleCurrencyChanged;
            CurrencyManager.Instance.OnCurrencyChanged += HandleCurrencyChanged;
        }

        if (_popTimer > 0f)
            _popTimer -= Time.unscaledDeltaTime;

        _displayedCurrency = CurrencyManager.Instance != null
            ? CurrencyManager.Instance.TeamCurrency
            : 0;
    }

    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _currencyStyle = new GUIStyle
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperRight
        };
        _currencyStyle.normal.textColor = new Color(0.2f, 1f, 0.2f);

        _popStyle = new GUIStyle
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperRight
        };
        _popStyle.normal.textColor = new Color(1f, 1f, 0.3f);
    }

    private void OnGUI()
    {
        if (CurrencyManager.Instance == null) return;

        InitStyles();

        float x = Screen.width - 220f;
        float y = 15f;
        float w = 200f;

        // Main currency display
        GUI.Label(new Rect(x, y, w, 30f), $"${_displayedCurrency}", _currencyStyle);

        // Pop-up showing last earned amount
        if (_popTimer > 0f && _lastAwardAmount > 0)
        {
            float alpha = Mathf.Clamp01(_popTimer / PopDuration);
            var color = _popStyle!.normal.textColor;
            color.a = alpha;
            _popStyle.normal.textColor = color;

            float offsetY = (1f - alpha) * -15f;
            GUI.Label(new Rect(x, y + 30f + offsetY, w, 25f), $"+${_lastAwardAmount}", _popStyle);
        }
    }

    private void HandleCurrencyChanged(int oldValue, int newValue)
    {
        int delta = newValue - oldValue;
        if (delta > 0)
        {
            _lastAwardAmount = delta;
            _popTimer = PopDuration;
        }
    }
}
