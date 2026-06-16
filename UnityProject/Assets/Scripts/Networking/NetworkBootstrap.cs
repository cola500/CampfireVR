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
    public enum Phase { Idle, Hosting, Connecting, Connected }

    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private ushort port = 7777;
    [SerializeField] private Mode mode = Mode.Lan;

    public Mode CurrentMode => mode;
    public string CurrentModeLabel => mode == Mode.Relay ? "Internet" : "Same Wi-Fi";
    public string CurrentState => _state;
    public string LastButton => _lastButton;
    public string LastAction => _lastAction;
    public char CurrentLetter => _codeChars[0];
    public string CurrentRoom => new string(_codeChars);
    public bool LeftHandValid => InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).isValid;
    public bool RightHandValid => InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid;
    public string HostedAlias => _hostedAlias;
    public bool IsBusy => _busy;

    public Phase CurrentPhase
    {
        get
        {
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
    private const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int CodeLength = 1;
    private const string RelayCodeProperty = "rc";
    private const float StickDeadzone = 0.5f;
    private const float StickRepeatDelay = 0.35f;
    private const float StickRepeatInterval = 0.12f;
    // Long-press Y for this duration triggers an in-VR Stop. Short tap
    // still toggles mode — we delay the ToggleMode to release-edge so we
    // can suppress it if the press grew into a long-press.
    private const float StopLongPressDuration = 1.5f;

    // Host-side property write + verify constants. Photon's
    // SetCustomProperties is fire-and-forget (returns true once queued,
    // not on server ack) — observed in the 2026-06-15 two-headset session
    // that the host saw relay_host_ready but the joiner saw `rc` missing
    // on three independent attempts. We now read the property back from
    // our own CurrentRoom view, with retries, before claiming readiness.
    private const int RelayPropertyMaxAttempts = 3;
    private const int RelayPropertyVerifyDelayMs = 250;
    private const int RelayPropertyRetryDelayMs = 500;
    // Joiner waits this long for the property to appear in its local
    // CustomProperties view. Bumped from 5 s to 8 s to give Photon
    // more slack on slower connections; host verify worst case is
    // ~2.25 s so the joiner still has ~5 s of cushion when the host
    // succeeded.
    private const float RelayJoinPropertyTimeoutSec = 8f;

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

    // One slot: the room is a single letter A-Z. Default 'A' so a fresh
    // launch can host / join without the user touching anything.
    private readonly char[] _codeChars = { 'A' };

    private bool _prevStickPos, _prevStickNeg;
    private float _stickPosHeld, _stickNegHeld;
    private float _stickPosNextRepeat, _stickNegNextRepeat;

    // Y-button hold state for long-press Stop. _yHeldTime accumulates while
    // Y is down; once it crosses StopLongPressDuration, we fire Stop() and
    // mark _yConsumedByLongPress so the upcoming release-edge skips the
    // normal ToggleMode action.
    private bool _yHeld;
    private float _yHeldTime;
    private bool _yConsumedByLongPress;

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
            // Axis A: hook NGO's own transport-failure callback so we can
            // surface the failure state and reset our internal flags instead
            // of being silently stuck in "Waiting for friend" while NGO
            // shuts down underneath us.
            nm.OnTransportFailure += OnTransportFailure;
        }
        DebugLogger.Log("network_bootstrap_ready", null,
            ("mode", mode.ToString()),
            ("room", CurrentLetter.ToString()),
            ("scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
    }

    void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
            nm.OnTransportFailure -= OnTransportFailure;
        }
    }

    // Axis A: NGO's `UnityTransport` raised a fatal failure (e.g. Unity
    // Relay reported the allocation needs to be recreated). NGO has
    // already shut down by the time this fires; our job is to surface
    // a clear state to the user and reset internal flags so the next
    // host/join attempt starts from a clean slate.
    void OnTransportFailure()
    {
        var nm = NetworkManager.Singleton;
        bool wasHost = nm != null && nm.IsHost;
        DebugLogger.Log("ngo_transport_failure_detected", null,
            ("mode", mode.ToString()),
            ("was_host", wasHost));
        _state = "Connection dropped — press X then try again";
        _busy = false;
        _hostedAlias = "";
        _joinCodeInput = "";
    }

    void OnClientConnected(ulong id)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        if (nm.IsHost && id != nm.LocalClientId) _state = "Friend joined";
        else if (nm.IsClient && id == nm.LocalClientId) _state = "Connected";
        DebugLogger.Log("client_connected", null, ("id", (long)id), ("role", nm.IsHost ? "host" : "client"));
    }

    void OnClientDisconnected(ulong id)
    {
        _state = "Friend left";
        DebugLogger.Log("client_disconnected", null, ("id", (long)id));
    }

    void Update()
    {
        if (Application.isEditor)
        {
            if (Input.GetKeyDown(KeyCode.H)) { DebugLogger.Log("editor_key", "H"); StartHost(); }
            if (Input.GetKeyDown(KeyCode.C)) { DebugLogger.Log("editor_key", "C"); StartClient(); }
            if (Input.GetKeyDown(KeyCode.X)) { DebugLogger.Log("editor_key", "X"); Stop(); }
            if (Input.GetKeyDown(KeyCode.M)) { DebugLogger.Log("editor_key", "M"); ToggleMode(); }
            if (Input.GetKeyDown(KeyCode.L)) DebugLogger.Marker("editor_L");
        }

        // LeftHand secondary (Y) is handled by PollYLongPress below — we
        // need release-edge for the normal ToggleMode action so a long-press
        // can claim the press without also firing the mode toggle.
        PollController(XRNode.LeftHand,  ref _prevLPrimary, ref _prevLSecondary, OnLeftPrimary,  null);
        PollController(XRNode.RightHand, ref _prevRPrimary, ref _prevRSecondary, OnRightPrimary, OnRightSecondary);

        PollYLongPress();

        // Stick cycles the room letter at any time — there's no separate
        // "change room" mode. Default 'A' covers the no-touch case.
        UpdateStickCycle();
    }

    // Y on the left controller: short tap = ToggleMode (as before); hold
    // for >= StopLongPressDuration = Stop. Suppresses ToggleMode on release
    // if Stop already fired during the hold.
    void PollYLongPress()
    {
        var dev = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!dev.isValid)
        {
            _yHeld = false; _yHeldTime = 0f; _yConsumedByLongPress = false;
            return;
        }
        dev.TryGetFeatureValue(CommonUsages.secondaryButton, out bool yNow);

        if (yNow && !_yHeld)
        {
            // Press edge — start tracking; do not invoke ToggleMode yet.
            _yHeld = true;
            _yHeldTime = 0f;
            _yConsumedByLongPress = false;
        }
        if (yNow)
        {
            _yHeldTime += Time.deltaTime;
            if (!_yConsumedByLongPress && _yHeldTime >= StopLongPressDuration)
            {
                _yConsumedByLongPress = true;
                _lastButton = "LeftHand secondary";
                _lastAction = "Y long-press: stop session";
                DebugLogger.Log("stop_requested", "Y long-press");
                Stop();
            }
        }
        else if (_yHeld)
        {
            // Release edge — only fire ToggleMode if it wasn't a long-press.
            _yHeld = false;
            if (!_yConsumedByLongPress)
            {
                _lastButton = "LeftHand secondary";
                OnLeftSecondary();
            }
            _yHeldTime = 0f;
            _yConsumedByLongPress = false;
        }
    }

    void UpdateStickCycle()
    {
        var rDev = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        Vector2 stick = Vector2.zero;
        if (rDev.isValid) rDev.TryGetFeatureValue(CommonUsages.primary2DAxis, out stick);

        bool pos = false, neg = false;
        if (stick.sqrMagnitude >= StickDeadzone * StickDeadzone)
        {
            float val = Mathf.Abs(stick.x) >= Mathf.Abs(stick.y) ? stick.x : stick.y;
            pos = val > 0f;
            neg = val < 0f;
        }

        TickStick(pos, ref _prevStickPos, ref _stickPosHeld, ref _stickPosNextRepeat, +1);
        TickStick(neg, ref _prevStickNeg, ref _stickNegHeld, ref _stickNegNextRepeat, -1);
    }

    void TickStick(bool active, ref bool prev, ref float heldTime, ref float nextRepeat, int delta)
    {
        if (active && !prev)
        {
            _lastButton = "RightHand stick";
            _lastAction = delta > 0 ? "Stick: next room" : "Stick: prev room";
            CycleLetter(delta);
            heldTime = 0f;
            nextRepeat = 0f;
        }
        if (active)
        {
            heldTime += Time.deltaTime;
            if (heldTime > StickRepeatDelay && heldTime - nextRepeat >= StickRepeatInterval)
            {
                CycleLetter(delta);
                nextRepeat = heldTime;
            }
        }
        else
        {
            heldTime = 0f;
            nextRepeat = 0f;
        }
        prev = active;
    }

    void OnLeftPrimary()
    {
        _lastAction = $"X: host room {CurrentLetter}";
        StartHost();
    }

    void OnLeftSecondary()
    {
        _lastAction = "Y: toggle mode";
        ToggleMode();
    }

    void OnRightPrimary()
    {
        _lastAction = "A: recenter";
        Recenter();
    }

    void OnRightSecondary()
    {
        // Guard against confusing "join while already hosting" — the Unity
        // Services Multiplayer SDK throws SessionConflict ("player is already
        // a member of the lobby") if a host presses B on their own room.
        // Headset-observed regression in the 2026-05-16 fika test.
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsHost)
        {
            _lastAction = "B: ignored (already hosting)";
            _state = $"Already hosting Room {CurrentLetter}";
            DebugLogger.Log("join_ignored_already_hosting", null, ("room", CurrentLetter.ToString()));
            return;
        }
        if (mode == Mode.Relay && _services != null && _services.InRelaySession)
        {
            _lastAction = "B: ignored (already in session)";
            _state = $"Already in Room {CurrentLetter}";
            DebugLogger.Log("join_ignored_already_in_session", null, ("room", CurrentLetter.ToString()));
            return;
        }

        if (mode == Mode.Lan) _lastAction = "B: join LAN";
        else _lastAction = $"B: join room {CurrentLetter}";
        StartClient();
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

    // Axis A: if a session is live when the user toggles mode, tear it
    // down first. Verified 2026-06-15 (backlog row 4.102): without this
    // guard, ToggleMode flips the enum but leaves the active NGO/Relay/
    // Photon Voice session running, causing the next host/join press to
    // race a half-up old session.
    async void ToggleMode()
    {
        var nm = NetworkManager.Singleton;
        bool inNgoSession = nm != null && (nm.IsHost || nm.IsClient);
        bool inRelaySession = _services != null && _services.InRelaySession;
        if (inNgoSession || inRelaySession)
        {
            DebugLogger.Log("mode_toggle_stop_first", null,
                ("mode", mode.ToString()),
                ("in_ngo_session", inNgoSession),
                ("in_relay_session", inRelaySession));
            await StopAsync();
        }
        mode = (mode == Mode.Lan) ? Mode.Relay : Mode.Lan;
        _state = $"Mode · {CurrentModeLabel}";
        DebugLogger.Log("mode_changed", null, ("mode", mode.ToString()));
    }

    void Recenter()
    {
        var subs = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        foreach (var s in subs) s.TryRecenter();
        _state = "Recentered";
        DebugLogger.Log("recenter");
    }

    void CycleLetter(int delta)
    {
        char c = _codeChars[0];
        int i = CodeAlphabet.IndexOf(c);
        if (i < 0) i = 0;
        i = ((i + delta) % CodeAlphabet.Length + CodeAlphabet.Length) % CodeAlphabet.Length;
        _codeChars[0] = CodeAlphabet[i];
        _state = $"Room {_codeChars[0]}";
        DebugLogger.Log("room_changed", null, ("room", _codeChars[0].ToString()));
    }

    async void StartHost()
    {
        if (_busy) return;
        DebugLogger.Log("host_pressed", null, ("mode", mode.ToString()), ("room", CurrentLetter.ToString()));
        if (mode == Mode.Lan)
        {
            _state = "Lighting LAN fire";
            DebugLogger.Log("lan_host_attempt", null, ("address", serverAddress), ("port", (int)port));
            if (!ConfigureLanTransport()) { DebugLogger.Log("lan_host_failed", "transport-config"); return; }
            if (NetworkManager.Singleton.StartHost())
            {
                _state = "Waiting for friend";
                _voiceBootstrap?.JoinRoom(LanRoomName);
                DebugLogger.Log("lan_host_ready");
            }
            else { _state = "Host failed"; DebugLogger.Log("lan_host_failed", "StartHost-returned-false"); }
            return;
        }

        if (_services == null || !_services.IsReady) { _state = "Signing in"; DebugLogger.Log("relay_host_blocked", "services-not-ready"); return; }

        // Axis A: Photon Voice preflight. Without this guard, JoinRoom
        // queued via `_pendingRoom` and the host would silently wait up to
        // `WaitForRoomJoinedAsync`'s 8 s for a state that may never arrive
        // (verified 2026-06-15 Henrik join: "JoinOrCreateRoom can't be
        // sent because peer is not connected"). Surface the offline state
        // immediately and try one reconnect before failing.
        if (_voiceBootstrap != null && !_voiceBootstrap.IsConnectedToMaster)
        {
            DebugLogger.Log("relay_host_voice_offline", null,
                ("state", _voiceBootstrap.CurrentState));
            _voiceBootstrap.TryReconnect();
            bool reconnected = await _voiceBootstrap.WaitForConnectedAsync(5f);
            if (!reconnected)
            {
                _state = "Voice offline — try again";
                DebugLogger.Log("relay_host_voice_reconnect_failed", null,
                    ("state", _voiceBootstrap.CurrentState));
                return;
            }
        }

        _busy = true;
        _state = "Creating fire";
        DebugLogger.Log("relay_host_attempt", null, ("room", CurrentLetter.ToString()));

        var realCode = await _services.HostRelayAsync();
        if (string.IsNullOrEmpty(realCode))
        {
            _busy = false;
            _state = "Couldn't start fire";
            DebugLogger.Log("relay_alloc_failed");
            return;
        }
        DebugLogger.Log("relay_alloc_succeeded");

        // Host advertises the current room letter (default 'A') as the
        // human-facing alias. Voice room name and discovery key both
        // key off this single letter.
        _hostedAlias = CurrentRoom;
        _state = "Sharing room";
        _voiceBootstrap?.JoinRoom(_hostedAlias);

        // Voice must be in the room before we can write the Relay code as
        // a custom property on it — that's how the joiner picks it up.
        bool roomReady = false;
        if (_voiceBootstrap != null) roomReady = await _voiceBootstrap.WaitForRoomJoinedAsync(8f);
        if (!roomReady)
        {
            _busy = false;
            _state = "Voice room failed";
            DebugLogger.Log("relay_host_voice_failed");
            return;
        }

        // Set + verify the rc property locally before claiming the host is
        // ready. If verify fails after retries, the joiner would just see
        // "Host's code missing" — better to surface that on the host side
        // so the user can long-press-Y and try again instead of waiting.
        // Set + verify on LocalPlayer.CustomProperties is now retained
        // as a diagnostic: it confirms the host can write its own player
        // state. The actual joiner discovery happens via Photon events
        // (PublishRelayCodeToJoiners below + VoiceBootstrap's
        // OnPlayerEnteredRoom forwarder).
        bool propertyVerified = await SetAndVerifyRelayCodeAsync(realCode);
        _voiceBootstrap?.PublishRelayCodeToJoiners(realCode);

        _busy = false;
        if (propertyVerified)
        {
            _state = "Waiting for friend";
            DebugLogger.Log("relay_host_ready");
        }
        else
        {
            _state = "Host code didn't sync — long-press Y, try again";
            DebugLogger.Log("relay_host_player_property_set_failed", null,
                ("room", CurrentLetter.ToString()));
        }
    }

    // Set the Relay join code as a custom property on our own LocalPlayer
    // (the host's player object in the Photon voice room), then verify
    // by reading it back from LocalPlayer.CustomProperties. Retries up
    // to RelayPropertyMaxAttempts; each attempt is queue → wait
    // RelayPropertyVerifyDelayMs → read back. Emits one log event per
    // step so the JSONL trail makes it obvious which step failed.
    //
    // Player properties (rather than room properties) are the discovery
    // channel because Photon Voice's LoadBalancingClient does not
    // propagate room custom properties to new joiners (verified
    // 2026-06-15). Player properties broadcast unconditionally to all
    // room members on join and on every update.
    //
    // Returns true if a read-back saw the code we wrote; false if all
    // attempts failed (queue refused, or queued but never visible).
    async Task<bool> SetAndVerifyRelayCodeAsync(string code)
    {
        if (_voiceBootstrap == null) return false;

        for (int attempt = 1; attempt <= RelayPropertyMaxAttempts; attempt++)
        {
            DebugLogger.Log("relay_host_player_property_set_attempt", null,
                ("attempt", attempt), ("key", RelayCodeProperty));
            bool queued = _voiceBootstrap.SetLocalPlayerProperty(RelayCodeProperty, code);
            DebugLogger.Log("relay_host_player_property_set_result", null,
                ("attempt", attempt), ("queued", queued));

            if (!queued)
            {
                await Task.Delay(RelayPropertyRetryDelayMs);
                continue;
            }

            DebugLogger.Log("relay_host_player_property_verify_attempt", null,
                ("attempt", attempt));
            await Task.Delay(RelayPropertyVerifyDelayMs);

            string readBack = _voiceBootstrap.GetLocalPlayerProperty(RelayCodeProperty);
            if (readBack == code)
            {
                DebugLogger.Log("relay_host_player_property_verify_succeeded", null,
                    ("attempt", attempt));
                return true;
            }

            DebugLogger.Log("relay_host_player_property_verify_failed", null,
                ("attempt", attempt),
                ("read_back_was", string.IsNullOrEmpty(readBack) ? "null" : "different"));
            await Task.Delay(RelayPropertyRetryDelayMs);
        }
        return false;
    }

    async void StartClient()
    {
        if (_busy) return;
        DebugLogger.Log("join_pressed", null, ("mode", mode.ToString()), ("room", CurrentLetter.ToString()));
        if (mode == Mode.Lan)
        {
            _state = "Joining fire";
            DebugLogger.Log("lan_join_attempt", null, ("address", serverAddress), ("port", (int)port));
            if (!ConfigureLanTransport()) { DebugLogger.Log("lan_join_failed", "transport-config"); return; }
            if (NetworkManager.Singleton.StartClient())
            {
                _state = "Joining fire";
                _voiceBootstrap?.JoinRoom(LanRoomName);
                DebugLogger.Log("lan_join_started");
            }
            else { _state = "Join failed"; DebugLogger.Log("lan_join_failed", "StartClient-returned-false"); }
            return;
        }

        if (_services == null || !_services.IsReady) { _state = "Signing in"; DebugLogger.Log("relay_join_blocked", "services-not-ready"); return; }

        // Axis A: Photon Voice preflight, mirror of StartHost.
        if (_voiceBootstrap != null && !_voiceBootstrap.IsConnectedToMaster)
        {
            DebugLogger.Log("relay_join_voice_offline", null,
                ("state", _voiceBootstrap.CurrentState));
            _voiceBootstrap.TryReconnect();
            bool reconnected = await _voiceBootstrap.WaitForConnectedAsync(5f);
            if (!reconnected)
            {
                _state = "Voice offline — try again";
                DebugLogger.Log("relay_join_voice_reconnect_failed", null,
                    ("state", _voiceBootstrap.CurrentState));
                return;
            }
        }

        // Always join the currently selected room letter (default 'A').
        _joinCodeInput = CurrentRoom;
        var alias = _joinCodeInput;
        _busy = true;
        _state = $"Looking for room {alias}";
        DebugLogger.Log("relay_join_attempt", null, ("room", alias));

        _voiceBootstrap?.JoinRoom(alias);

        bool joined = false;
        if (_voiceBootstrap != null) joined = await _voiceBootstrap.WaitForRoomJoinedAsync(8f);
        if (!joined)
        {
            _busy = false;
            _state = "No fire found";
            DebugLogger.Log("relay_join_voice_timeout", null, ("room", alias));
            return;
        }

        // Discovery via Photon events (Plan C1): joiner sends a request,
        // host either broadcasts on OnPlayerEnteredRoom or responds to
        // the request. Whichever event arrives first wins.
        var realCode = await _voiceBootstrap.WaitForRelayCodeEventAsync(RelayJoinPropertyTimeoutSec);
        if (string.IsNullOrEmpty(realCode))
        {
            _busy = false;
            _state = "Host's code missing";
            // Timeout event is already logged by WaitForRelayCodeEventAsync.
            return;
        }

        _state = "Joining fire";
        DebugLogger.Log("relay_join_calling");
        bool ok = await _services.JoinRelayAsync(realCode);
        _busy = false;
        if (!ok)
        {
            _state = "Couldn't reach fire";
            DebugLogger.Log("relay_join_failed");
            // Axis A: leave the Photon voice room so the next press of B
            // starts from a clean slate instead of racing a half-joined
            // state. Preserves the failure state text.
            await ResetAfterFailedJoinAsync();
            _state = "Couldn't reach fire";
        }
        else DebugLogger.Log("relay_join_succeeded");
    }

    // Fire-and-forget wrapper for callers that don't need to await the
    // teardown (the Y long-press in-VR and the X key in Editor).
    void Stop() { _ = StopAsync(); }

    // Awaitable version — used by ToggleMode and post-failed-join paths
    // that need to wait for the teardown to finish before continuing.
    async Task StopAsync()
    {
        // Caller (long-press Y in-VR, X-key in Editor) already logged
        // stop_requested with its source. Tear down voice → Relay → NGO in
        // that order; each step swallows its own errors so a partial fail
        // doesn't block the rest of the recovery.
        bool clean = true;
        _voiceBootstrap?.ClearPublishedRelayCode();
        try { _voiceBootstrap?.LeaveRoom(); }
        catch (System.Exception e) { clean = false; DebugLogger.Log("stop_step_failed", "voice_leave", ("error", e.Message)); }

        if (_services != null && _services.InRelaySession)
        {
            try { await _services.LeaveRelayAsync(); }
            catch (System.Exception e) { clean = false; DebugLogger.Log("stop_step_failed", "relay_leave", ("error", e.Message)); }
        }

        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            try
            {
                // Axis A follow-up: NGO `Shutdown()` is non-blocking — it
                // enqueues the teardown which completes over multiple frames.
                // Without waiting, the next `StartHost` / `StartClient` (or
                // Unity Multiplayer SDK's `JoinSessionByCodeAsync`) can race
                // a partially-shut-down NetworkManager / UnityTransport and
                // throw `SessionException [Error: NetworkSetupFailed]`.
                // Verified 2026-06-15 22:36 session: A2 without force-stop
                // failed with that exact exception.
                //
                // Important: Unity Multiplayer Sessions SDK's
                // `_session.LeaveAsync()` (called in `LeaveRelayAsync` above)
                // *internally* triggers NGO Shutdown — by the time we get
                // here, `IsHost` / `IsClient` are often already false but
                // `ShutdownInProgress` is still true. So always poll
                // regardless of host/client flags. We only call `Shutdown()`
                // ourselves if Sessions SDK hasn't already started it.
                if (nm.IsHost || nm.IsClient) nm.Shutdown();
                DebugLogger.Log("ngo_shutdown_wait_started", null,
                    ("was_host", nm.IsHost),
                    ("was_client", nm.IsClient),
                    ("in_progress_at_check", nm.ShutdownInProgress));
                const float maxWaitSeconds = 2f;
                const int pollMs = 50;
                float waited = 0f;
                while (nm.ShutdownInProgress && waited < maxWaitSeconds)
                {
                    await Task.Delay(pollMs);
                    waited += pollMs / 1000f;
                }
                if (nm.ShutdownInProgress)
                {
                    clean = false;
                    DebugLogger.Log("ngo_shutdown_wait_timeout", null,
                        ("waited_seconds", waited));
                }
                else
                {
                    DebugLogger.Log("ngo_shutdown_wait_completed", null,
                        ("waited_seconds", waited));
                }
            }
            catch (System.Exception e) { clean = false; DebugLogger.Log("stop_step_failed", "ngo_shutdown", ("error", e.Message)); }
        }

        // Reset local UI state but preserve the user's chosen room letter
        // and mode so they don't have to re-pick when they host/join again.
        _joinCodeInput = "";
        _hostedAlias = "";
        _busy = false;

        _state = "Stopped session";
        DebugLogger.Log(clean ? "stop_completed" : "stop_completed_with_errors", null,
            ("mode", mode.ToString()), ("room", CurrentLetter.ToString()));
    }

    // Axis A: lighter teardown after a failed Relay join. The joiner never
    // entered the room from NGO's perspective so we don't shutdown NGO;
    // we just leave the Photon voice room (so a retry doesn't race a
    // half-joined state) and reset our internal flags. Caller preserves
    // the failure state text ("Couldn't reach fire") that the user just saw.
    async Task ResetAfterFailedJoinAsync()
    {
        DebugLogger.Log("relay_join_cleanup_after_fail");
        try { _voiceBootstrap?.LeaveRoom(); }
        catch (System.Exception e)
        { DebugLogger.Log("stop_step_failed", "voice_leave_after_failed_join", ("error", e.Message)); }
        _joinCodeInput = "";
        _busy = false;
        // Yield once so the Photon LeaveRoom op gets queued before we return.
        await Task.Yield();
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
            GUI.Label(new Rect(0, topY, w, 40), "ROOM", _labelStyle);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.5f);
            var prev = GUI.color;
            GUI.color = Color.Lerp(new Color(1f, 0.72f, 0.42f), new Color(1f, 0.88f, 0.62f), pulse);
            GUI.Label(new Rect(0, topY + 50, w, 140), _hostedAlias, _codeStyle);
            GUI.color = prev;
        }
        else if (!connected)
        {
            GUI.Label(new Rect(0, topY, w, 40), "ROOM", _labelStyle);
            GUI.Label(new Rect(0, topY + 50, w, 140), CurrentRoom, _codeStyle);
        }

        GUI.Label(new Rect(0, Screen.height * 0.70f, w, 40), _state, _stateStyle);

        if (mode == Mode.Relay && Application.isEditor && !connected)
        {
            GUI.Label(new Rect(20, 80, 220, 28), "Editor room override:");
            var typed = (GUI.TextField(new Rect(240, 80, 60, 28), CurrentRoom, CodeLength) ?? "").ToUpper();
            if (!string.IsNullOrEmpty(typed) && CodeAlphabet.IndexOf(typed[0]) >= 0)
                _codeChars[0] = typed[0];
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
