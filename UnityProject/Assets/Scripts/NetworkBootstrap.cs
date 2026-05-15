using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.XR;

public class NetworkBootstrap : MonoBehaviour
{
    public enum Mode { Lan, Relay }

    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private ushort port = 7777;
    [SerializeField] private Mode mode = Mode.Lan;

    public Mode CurrentMode => mode;
    public string CurrentState => _state;
    public string LastButton => _lastButton;

    private const string LanRoomName = "lan-campfire";

    private string _joinCodeInput = "";
    private string _state = "Idle";
    private string _lastButton = "";
    private bool _prevLPrimary, _prevLSecondary, _prevRPrimary, _prevRSecondary;
    private bool _loggedLeftInvalid, _loggedRightInvalid;
    private ServicesBootstrap _services;
    private VoiceBootstrap _voiceBootstrap;
    private bool _busy;
    private TouchScreenKeyboard _kbd;

    private GUIStyle _codeStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _stateStyle;
    private GUIStyle _promptStyle;
    private GUIStyle _modeStyle;

    void Awake()
    {
        _services = GetComponent<ServicesBootstrap>();
        _voiceBootstrap = GetComponent<VoiceBootstrap>();
    }

    void Start()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    void OnClientConnected(ulong id)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        if (nm.IsHost && id != nm.LocalClientId) _state = "Friend joined the fire";
        else if (nm.IsClient && id == nm.LocalClientId) _state = "Connected";
    }

    void OnClientDisconnected(ulong id)
    {
        _state = "Disconnected";
    }

    void Update()
    {
        if (Application.isEditor)
        {
            if (Input.GetKeyDown(KeyCode.H)) StartHost();
            if (Input.GetKeyDown(KeyCode.C)) StartClient();
            if (Input.GetKeyDown(KeyCode.X)) Stop();
            if (Input.GetKeyDown(KeyCode.M)) ToggleMode();
        }

        PollController(XRNode.LeftHand,  ref _prevLPrimary, ref _prevLSecondary, StartHost, ToggleMode);
        PollController(XRNode.RightHand, ref _prevRPrimary, ref _prevRSecondary, Recenter,  StartClient);

        if (_kbd != null)
        {
            if (_kbd.status == TouchScreenKeyboard.Status.Done)
            {
                _joinCodeInput = (_kbd.text ?? "").Trim().ToUpper();
                _kbd = null;
                if (!string.IsNullOrEmpty(_joinCodeInput)) StartClient();
                else _state = "No code entered";
            }
            else if (_kbd.status != TouchScreenKeyboard.Status.Visible)
            {
                _kbd = null;
            }
        }
    }

    void PollController(XRNode node, ref bool prevP, ref bool prevS, System.Action onPrimary, System.Action onSecondary)
    {
        var dev = InputDevices.GetDeviceAtXRNode(node);
        ref bool loggedInvalid = ref (node == XRNode.LeftHand ? ref _loggedLeftInvalid : ref _loggedRightInvalid);

        if (!dev.isValid)
        {
            if (!loggedInvalid)
            {
                Debug.Log($"[Ctrl] {node} device not valid yet (controller off or not paired)");
                loggedInvalid = true;
            }
            return;
        }
        if (loggedInvalid)
        {
            Debug.Log($"[Ctrl] {node} device valid: {dev.name} / {dev.manufacturer} / {dev.characteristics}");
            loggedInvalid = false;
        }

        dev.TryGetFeatureValue(CommonUsages.primaryButton, out bool p);
        dev.TryGetFeatureValue(CommonUsages.secondaryButton, out bool s);
        if (p && !prevP)
        {
            _lastButton = $"{node} primary";
            Debug.Log($"[Ctrl] {node} primary pressed");
            onPrimary?.Invoke();
        }
        if (s && !prevS)
        {
            _lastButton = $"{node} secondary";
            Debug.Log($"[Ctrl] {node} secondary pressed");
            onSecondary?.Invoke();
        }
        prevP = p; prevS = s;
    }

    void ToggleMode()
    {
        mode = (mode == Mode.Lan) ? Mode.Relay : Mode.Lan;
        _state = $"Mode: {mode}";
    }

    void Recenter()
    {
        var subs = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        foreach (var s in subs) s.TryRecenter();
        _state = "Recentered";
    }

    async void StartHost()
    {
        if (_busy) return;
        if (mode == Mode.Lan)
        {
            if (!ConfigureLanTransport()) return;
            if (NetworkManager.Singleton.StartHost())
            {
                _state = $"Waiting for friend on LAN :{port}…";
                _voiceBootstrap?.JoinRoom(LanRoomName);
            }
            else _state = "LAN host failed";
        }
        else
        {
            if (_services == null || !_services.IsReady) { _state = "Signing in…"; return; }
            _busy = true;
            _state = "Creating campfire session…";
            var code = await _services.HostRelayAsync();
            _busy = false;
            if (code != null)
            {
                _state = "Waiting for friend…";
                _voiceBootstrap?.JoinRoom(code);
            }
            else _state = "Relay host failed";
        }
    }

    async void StartClient()
    {
        if (_busy) return;
        if (mode == Mode.Lan)
        {
            if (!ConfigureLanTransport()) return;
            if (NetworkManager.Singleton.StartClient())
            {
                _state = $"Connecting → {serverAddress}…";
                _voiceBootstrap?.JoinRoom(LanRoomName);
            }
            else _state = "LAN client failed";
        }
        else
        {
            if (_services == null || !_services.IsReady) { _state = "Signing in…"; return; }
            if (string.IsNullOrEmpty(_joinCodeInput))
            {
                if (TouchScreenKeyboard.isSupported)
                {
                    _kbd = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default, false, false, false, false, "Campfire code");
                    _state = "Enter the campfire code…";
                }
                else
                {
                    _state = "Enter join code in field then press Join again";
                }
                return;
            }
            _busy = true;
            _state = $"Connecting to {_joinCodeInput}…";
            bool ok = await _services.JoinRelayAsync(_joinCodeInput);
            _busy = false;
            if (ok) _voiceBootstrap?.JoinRoom(_joinCodeInput);
            else _state = "Could not join — check the code";
        }
    }

    async void Stop()
    {
        _voiceBootstrap?.LeaveRoom();
        if (_services != null && _services.InRelaySession)
            await _services.LeaveRelayAsync();
        var nm = NetworkManager.Singleton;
        if (nm != null && (nm.IsHost || nm.IsClient)) nm.Shutdown();
        _state = "Disconnected";
        _joinCodeInput = "";
    }

    bool ConfigureLanTransport()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) { _state = "No NetworkManager"; return false; }
        var t = nm.GetComponent<UnityTransport>();
        if (t == null) { _state = "No UnityTransport"; return false; }
        t.SetConnectionData(serverAddress, port, "0.0.0.0");
        return true;
    }

    void EnsureStyles()
    {
        if (_codeStyle != null) return;
        _codeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 96, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
        };
        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28, alignment = TextAnchor.MiddleCenter,
        };
        _stateStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26, alignment = TextAnchor.MiddleCenter,
        };
        _promptStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
        };
        _promptStyle.normal.textColor = new Color(1f, 0.85f, 0.62f, 0.85f);
        _modeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18, alignment = TextAnchor.MiddleCenter,
        };
        _modeStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 0.6f);
    }

    static string SpacedCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return "";
        var sb = new StringBuilder(code.Length * 2);
        for (int i = 0; i < code.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(code[i]);
        }
        return sb.ToString();
    }

    void OnGUI()
    {
        EnsureStyles();

        var nm = NetworkManager.Singleton;
        bool connected = nm != null && (nm.IsHost || nm.IsClient);

        float w = Screen.width;
        GUI.Label(new Rect(0, 20, w, 24), $"Mode: {mode}", _modeStyle);

        if (Application.isEditor)
        {
            GUI.Label(new Rect(20, 50, 1100, 24),
                $"Local IPs: {string.Join(", ", GetLocalIPv4())}    (editor keys: H host, C join, M mode, X stop)");
        }

        float topY = Screen.height * 0.18f;
        if (mode == Mode.Relay && _services != null && _services.InRelaySession && !string.IsNullOrEmpty(_services.JoinCode))
        {
            GUI.Label(new Rect(0, topY, w, 40), "CAMPFIRE CODE", _labelStyle);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.5f);
            var prev = GUI.color;
            GUI.color = Color.Lerp(new Color(1f, 0.72f, 0.42f), new Color(1f, 0.88f, 0.62f), pulse);
            GUI.Label(new Rect(0, topY + 50, w, 140), SpacedCode(_services.JoinCode), _codeStyle);
            GUI.color = prev;
        }

        GUI.Label(new Rect(0, Screen.height * 0.70f, w, 40), _state, _stateStyle);

        if (!connected)
        {
            float py = Screen.height * 0.78f;
            GUI.Label(new Rect(0, py + 0,  w, 28), "PRESS X TO HOST",        _promptStyle);
            GUI.Label(new Rect(0, py + 28, w, 28), "PRESS B TO JOIN",        _promptStyle);
            GUI.Label(new Rect(0, py + 56, w, 28), "PRESS Y TO SWITCH MODE", _promptStyle);
            GUI.Label(new Rect(0, py + 84, w, 28), "PRESS A TO RECENTER",    _promptStyle);
        }

        if (mode == Mode.Relay && Application.isEditor)
        {
            GUI.Label(new Rect(20, 80, 220, 28), "Join code (editor):");
            _joinCodeInput = GUI.TextField(new Rect(240, 80, 220, 28), _joinCodeInput ?? "", 8).ToUpper();
        }
    }

    static IEnumerable<string> GetLocalIPv4()
    {
        IPHostEntry entry;
        try { entry = Dns.GetHostEntry(Dns.GetHostName()); } catch { yield break; }
        foreach (var ip in entry.AddressList)
            if (ip.AddressFamily == AddressFamily.InterNetwork) yield return ip.ToString();
    }
}
