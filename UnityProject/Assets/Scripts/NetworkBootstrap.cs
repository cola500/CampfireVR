using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.XR;

public class NetworkBootstrap : MonoBehaviour
{
    public enum Mode { Lan, Relay }
    public enum InputState { Idle, EditingCode }
    public enum Phase { Idle, Hosting, Joining, Connecting, Connected }

    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private ushort port = 7777;
    [SerializeField] private Mode mode = Mode.Lan;

    public Mode CurrentMode => mode;
    public string CurrentState => _state;
    public string LastButton => _lastButton;
    public string LastAction => _lastAction;
    public bool IsEditingCode => _inputState == InputState.EditingCode;
    public string CodeDisplay => FormatCode();
    public int CodeSlot => _slotIndex;
    public int CodeLengthSlots => CodeLength;
    public char CodeValue => _codeChars[Mathf.Clamp(_slotIndex, 0, CodeLength - 1)];
    public bool LeftHandValid => InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).isValid;
    public bool RightHandValid => InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid;
    public string HostedAlias => _hostedAlias;
    public bool IsBusy => _busy;

    public Phase CurrentPhase
    {
        get
        {
            if (_inputState == InputState.EditingCode) return Phase.Joining;
            var nm = NetworkManager.Singleton;
            if (nm == null) return Phase.Idle;
            if (nm.IsClient && !nm.IsHost)
                return nm.IsConnectedClient ? Phase.Connected : Phase.Connecting;
            if (nm.IsHost)
                return nm.ConnectedClientsIds.Count >= 2 ? Phase.Connected : Phase.Hosting;
            if (_busy && mode == Mode.Relay)
            {
                if (string.IsNullOrEmpty(_joinCodeInput)) return Phase.Hosting;
                return Phase.Connecting;
            }
            return Phase.Idle;
        }
    }

    private const string LanRoomName = "lan-campfire";
    private const string CodeAlphabet = "ABC";
    private const int CodeLength = 3;
    private const string RelayCodeProperty = "rc";
    private const float AutoRepeatDelay = 0.45f;
    private const float AutoRepeatInterval = 0.18f;

    private string _joinCodeInput = "";
    private string _state = "Idle";
    private string _lastButton = "";
    private string _lastAction = "";
    private string _hostedAlias = "";
    private bool _prevLPrimary, _prevLSecondary, _prevRPrimary, _prevRSecondary;
    private bool _loggedLeftInvalid, _loggedRightInvalid;
    private ServicesBootstrap _services;
    private VoiceBootstrap _voiceBootstrap;
    private bool _busy;

    private InputState _inputState = InputState.Idle;
    private readonly char[] _codeChars = { 'A', 'A', 'A' };
    private int _slotIndex = 0;

    private float _aHeldTime, _xHeldTime, _aNextRepeat, _xNextRepeat;

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
        if (nm.IsHost && id != nm.LocalClientId) _state = "Friend joined";
        else if (nm.IsClient && id == nm.LocalClientId) _state = "Connected";
    }

    void OnClientDisconnected(ulong id)
    {
        _state = "Friend left";
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

        PollController(XRNode.LeftHand,  ref _prevLPrimary, ref _prevLSecondary, OnLeftPrimary,  OnLeftSecondary);
        PollController(XRNode.RightHand, ref _prevRPrimary, ref _prevRSecondary, OnRightPrimary, OnRightSecondary);

        if (_inputState == InputState.EditingCode)
            UpdateAutoRepeat();
        else { _aHeldTime = _xHeldTime = _aNextRepeat = _xNextRepeat = 0f; }
    }

    void UpdateAutoRepeat()
    {
        var rDev = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        var lDev = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        bool aHeld = false, xHeld = false;
        if (rDev.isValid) rDev.TryGetFeatureValue(CommonUsages.primaryButton, out aHeld);
        if (lDev.isValid) lDev.TryGetFeatureValue(CommonUsages.primaryButton, out xHeld);

        TickRepeat(aHeld, ref _aHeldTime, ref _aNextRepeat, () => CycleSlot(+1));
        TickRepeat(xHeld, ref _xHeldTime, ref _xNextRepeat, () => CycleSlot(-1));
    }

    void TickRepeat(bool held, ref float heldTime, ref float nextRepeat, System.Action action)
    {
        if (!held) { heldTime = 0; nextRepeat = 0; return; }
        heldTime += Time.deltaTime;
        if (heldTime <= AutoRepeatDelay) return;
        if (heldTime - nextRepeat >= AutoRepeatInterval)
        {
            action();
            nextRepeat = heldTime;
        }
    }

    void OnLeftPrimary()
    {
        if (_inputState == InputState.EditingCode) { _lastAction = "X: prev letter"; CycleSlot(-1); }
        else { _lastAction = "X: host"; StartHost(); }
    }

    void OnLeftSecondary()
    {
        if (_inputState == InputState.EditingCode)
        {
            if (_slotIndex > 0)
            {
                _slotIndex--;
                _lastAction = $"Y: prev slot → {_slotIndex + 1}";
                _state = $"Slot {_slotIndex + 1} → {_codeChars[_slotIndex]}";
            }
            else { _lastAction = "Y: back"; _state = "Cancelled"; ExitCodeEditor(false); }
        }
        else { _lastAction = "Y: toggle mode"; ToggleMode(); }
    }

    void OnRightPrimary()
    {
        if (_inputState == InputState.EditingCode) { _lastAction = "A: next letter"; CycleSlot(+1); }
        else { _lastAction = "A: recenter"; Recenter(); }
    }

    void OnRightSecondary()
    {
        if (_inputState == InputState.EditingCode)
        {
            if (_slotIndex >= CodeLength - 1) { _lastAction = "B: join"; ExitCodeEditor(true); }
            else
            {
                _slotIndex++;
                _lastAction = $"B: next slot → {_slotIndex + 1}";
                _state = $"Slot {_slotIndex + 1} → {_codeChars[_slotIndex]}";
            }
        }
        else
        {
            if (mode == Mode.Lan) { _lastAction = "B: join LAN"; StartClient(); }
            else { _lastAction = "B: enter code"; EnterCodeEditor(); }
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
            onPrimary?.Invoke();
        }
        if (s && !prevS)
        {
            _lastButton = $"{node} secondary";
            onSecondary?.Invoke();
        }
        prevP = p; prevS = s;
    }

    void ToggleMode()
    {
        mode = (mode == Mode.Lan) ? Mode.Relay : Mode.Lan;
        _state = $"Mode · {mode}";
    }

    void Recenter()
    {
        var subs = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        foreach (var s in subs) s.TryRecenter();
        _state = "Recentered";
    }

    void EnterCodeEditor()
    {
        if (_busy) return;
        _inputState = InputState.EditingCode;
        _slotIndex = 0;
        _state = "Enter code";
    }

    void ExitCodeEditor(bool startClient)
    {
        _inputState = InputState.Idle;
        if (startClient)
        {
            _joinCodeInput = new string(_codeChars);
            StartClient();
        }
    }

    void CycleSlot(int delta)
    {
        char c = _codeChars[_slotIndex];
        int i = CodeAlphabet.IndexOf(c);
        if (i < 0) i = 0;
        i = ((i + delta) % CodeAlphabet.Length + CodeAlphabet.Length) % CodeAlphabet.Length;
        _codeChars[_slotIndex] = CodeAlphabet[i];
        _state = $"Slot {_slotIndex + 1} → {_codeChars[_slotIndex]}";
    }

    string FormatCode()
    {
        var sb = new StringBuilder(CodeLength * 4);
        for (int i = 0; i < CodeLength; i++)
        {
            if (i > 0) sb.Append(' ');
            if (i == _slotIndex && _inputState == InputState.EditingCode)
                sb.Append('[').Append(_codeChars[i]).Append(']');
            else
                sb.Append(' ').Append(_codeChars[i]).Append(' ');
        }
        return sb.ToString();
    }

    static string GenerateAlias()
    {
        var sb = new StringBuilder(CodeLength);
        for (int i = 0; i < CodeLength; i++)
            sb.Append(CodeAlphabet[Random.Range(0, CodeAlphabet.Length)]);
        return sb.ToString();
    }

    async void StartHost()
    {
        if (_busy) return;
        if (mode == Mode.Lan)
        {
            _state = "Lighting LAN fire";
            if (!ConfigureLanTransport()) return;
            if (NetworkManager.Singleton.StartHost())
            {
                _state = "Waiting for friend";
                _voiceBootstrap?.JoinRoom(LanRoomName);
            }
            else _state = "Host failed";
            return;
        }

        if (_services == null || !_services.IsReady) { _state = "Signing in"; return; }
        _busy = true;
        _state = "Creating fire";

        var realCode = await _services.HostRelayAsync();
        if (string.IsNullOrEmpty(realCode))
        {
            _busy = false;
            _state = "Couldn't start fire";
            return;
        }

        _hostedAlias = GenerateAlias();
        _state = "Sharing code";
        _voiceBootstrap?.JoinRoom(_hostedAlias);

        bool roomReady = false;
        if (_voiceBootstrap != null) roomReady = await _voiceBootstrap.WaitForRoomJoinedAsync(8f);
        if (roomReady) _voiceBootstrap.SetRoomProperty(RelayCodeProperty, realCode);

        _busy = false;
        _state = roomReady ? "Waiting for friend" : "Voice room failed";
    }

    async void StartClient()
    {
        if (_busy) return;
        if (mode == Mode.Lan)
        {
            _state = "Joining fire";
            if (!ConfigureLanTransport()) return;
            if (NetworkManager.Singleton.StartClient())
            {
                _state = "Joining fire";
                _voiceBootstrap?.JoinRoom(LanRoomName);
            }
            else _state = "Join failed";
            return;
        }

        if (_services == null || !_services.IsReady) { _state = "Signing in"; return; }
        if (string.IsNullOrEmpty(_joinCodeInput)) { _state = "No code"; return; }

        var alias = _joinCodeInput;
        _busy = true;
        _state = "Looking for fire";

        _voiceBootstrap?.JoinRoom(alias);

        bool joined = false;
        if (_voiceBootstrap != null) joined = await _voiceBootstrap.WaitForRoomJoinedAsync(8f);
        if (!joined)
        {
            _busy = false;
            _state = "No fire found";
            return;
        }

        var realCode = await _voiceBootstrap.WaitForRoomPropertyAsync(RelayCodeProperty, 5f);
        if (string.IsNullOrEmpty(realCode))
        {
            _busy = false;
            _state = "Host's code missing";
            return;
        }

        _state = "Joining fire";
        bool ok = await _services.JoinRelayAsync(realCode);
        _busy = false;
        if (!ok) _state = "Couldn't reach fire";
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
        _hostedAlias = "";
        _inputState = InputState.Idle;
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
        _codeStyle = new GUIStyle(GUI.skin.label) { fontSize = 96, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
        _stateStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
        _promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        _promptStyle.normal.textColor = new Color(1f, 0.85f, 0.62f, 0.85f);
        _modeStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
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
        if (!Application.isEditor) return;

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
        if (!string.IsNullOrEmpty(_hostedAlias))
        {
            GUI.Label(new Rect(0, topY, w, 40), "CAMPFIRE CODE", _labelStyle);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.5f);
            var prev = GUI.color;
            GUI.color = Color.Lerp(new Color(1f, 0.72f, 0.42f), new Color(1f, 0.88f, 0.62f), pulse);
            GUI.Label(new Rect(0, topY + 50, w, 140), SpacedCode(_hostedAlias), _codeStyle);
            GUI.color = prev;
        }

        GUI.Label(new Rect(0, Screen.height * 0.70f, w, 40), _state, _stateStyle);

        if (mode == Mode.Relay && Application.isEditor)
        {
            GUI.Label(new Rect(20, 80, 220, 28), "Join code (editor):");
            _joinCodeInput = (GUI.TextField(new Rect(240, 80, 120, 28), _joinCodeInput ?? "", CodeLength) ?? "").ToUpper();
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
