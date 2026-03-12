#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugPanel : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        var go = new GameObject("DebugPanel");
        go.AddComponent<DebugPanel>();
        DontDestroyOnLoad(go);
    }

    private bool _visible;
    private float _fps;
    private float _fpsTimer;
    private int _frameCount;

    private readonly GUIStyle _labelStyle = new();
    private readonly GUIStyle _headerStyle = new();
    private readonly GUIStyle _boxStyle = new();
    private bool _stylesInitialized;

    private const float PanelWidth = 320f;
    private const float Padding = 10f;

    private Texture2D _bgTex;
    private InteractionSystem.EquipmentHandler _cachedHandler;

    private void Update()
    {
        // Ctrl+D toggle
        var kb = Keyboard.current;
        if (kb != null && kb.leftCtrlKey.isPressed && kb.dKey.wasPressedThisFrame)
            _visible = !_visible;

        // FPS counter
        _frameCount++;
        _fpsTimer += Time.unscaledDeltaTime;
        if (_fpsTimer >= 0.5f)
        {
            _fps = _frameCount / _fpsTimer;
            _frameCount = 0;
            _fpsTimer = 0f;
        }
    }

    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _labelStyle.normal.textColor = Color.white;
        _labelStyle.fontSize = 14;

        _headerStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
        _headerStyle.fontSize = 14;
        _headerStyle.fontStyle = FontStyle.Bold;

        _bgTex = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.75f));
        _boxStyle.normal.background = _bgTex;
    }

    private void OnDestroy()
    {
        if (_bgTex != null)
            Destroy(_bgTex);
    }

    private void OnGUI()
    {
        if (!_visible) return;

        InitStyles();

        float y = Padding;
        float lineH = 20f;
        float sectionGap = 8f;

        float totalHeight = 300f;
        GUI.Box(new Rect(Padding - 5f, y - 5f, PanelWidth, totalHeight), GUIContent.none, _boxStyle);

        // --- FPS ---
        DrawLabel(ref y, lineH, $"FPS: {_fps:F0}");
        y += sectionGap;

        // --- Network ---
        DrawHeader(ref y, lineH, "Network");
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
        {
            DrawLabel(ref y, lineH, "  Not connected");
        }
        else
        {
            string role = nm.IsHost ? "Host" : nm.IsServer ? "Server" : "Client";
            DrawLabel(ref y, lineH, $"  Role: {role}");
            DrawLabel(ref y, lineH, $"  Client ID: {nm.LocalClientId}");

            if (nm.IsServer)
                DrawLabel(ref y, lineH, $"  Connected Clients: {nm.ConnectedClientsIds.Count}");

            if (!nm.IsServer)
            {
                var transport = nm.NetworkConfig?.NetworkTransport;
                if (transport != null)
                {
                    var rtt = transport.GetCurrentRtt(nm.LocalClientId);
                    DrawLabel(ref y, lineH, $"  RTT: {rtt} ms");
                }
            }
        }
        y += sectionGap;

        // --- Destruction System ---
        var mgr = DestructionNetworkManager.Instance;
        DrawHeader(ref y, lineH, "Destruction");
        if (mgr == null)
        {
            DrawLabel(ref y, lineH, "  Not initialized");
        }
        else
        {
            var registry = mgr.Registry;
            var replicator = mgr.Replicator;
            var broadcaster = mgr.Broadcaster;

            DrawLabel(ref y, lineH, $"  Registry Fragments: {registry?.Count ?? 0}");

            if (nm != null && nm.IsServer)
            {
                DrawLabel(ref y, lineH, $"  Demolitions (history): {replicator?.DemolitionHistory.Count ?? 0}");
                if (broadcaster != null)
                    DrawLabel(ref y, lineH, $"  Last Broadcast: {broadcaster.LastBroadcastCount} snapshots");
            }
            else if (broadcaster != null)
            {
                DrawLabel(ref y, lineH, $"  Interp Targets: {broadcaster.InterpolationTargetCount}");
            }
        }
        y += sectionGap;

        // --- Player ---
        DrawHeader(ref y, lineH, "Player");
        if (nm != null && nm.IsListening && nm.LocalClient?.PlayerObject != null)
        {
            var player = nm.LocalClient.PlayerObject;
            var pos = player.transform.position;
            DrawLabel(ref y, lineH, $"  Pos: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");

            if (_cachedHandler == null)
                _cachedHandler = player.GetComponent<InteractionSystem.EquipmentHandler>();

            if (_cachedHandler != null)
            {
                string item = _cachedHandler.HasEquipped ? _cachedHandler.CurrentUsable.UsableName : "None";
                DrawLabel(ref y, lineH, $"  Equipped: {item}");
            }
        }
        else
        {
            DrawLabel(ref y, lineH, "  No local player");
        }
    }

    private void DrawLabel(ref float y, float h, string text)
    {
        GUI.Label(new Rect(Padding, y, PanelWidth - Padding * 2f, h), text, _labelStyle);
        y += h;
    }

    private void DrawHeader(ref float y, float h, string text)
    {
        GUI.Label(new Rect(Padding, y, PanelWidth - Padding * 2f, h), text, _headerStyle);
        y += h;
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
        var tex = new Texture2D(w, h);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
#endif
