using System.Threading.Tasks;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.Unity;
using UnityEngine;

[RequireComponent(typeof(VoiceConnection))]
public class VoiceBootstrap : MonoBehaviour, IInRoomCallbacks, IOnEventCallback
{
    private VoiceConnection _voice;
    private string _status = "Voice: idle";
    private string _pendingRoom;
    private bool _inRoom;
    private bool _callbacksRegistered;
    private ClientState _lastLoggedState = ClientState.PeerCreated;

    // Axis A: focus/disconnect reconnect tracking. Single-shot per trigger;
    // never loops. See `Update()` for the trigger edges.
    private bool _wasFocused = true;
    private bool _reconnectScheduled;
    private bool _disconnectedWhileInRoom;
    // Tracks the last ConnectUsingSettings() call so we can log
    // voice_reconnect_succeeded/_failed when we transition back to
    // ConnectedToMasterServer (or never make it there before next attempt).
    private bool _reconnectInFlight;
    private const float IdleReconnectBackoffSeconds = 5f;

    // One-shot flag for region logging — fires the first time we reach
    // ConnectedToMasterServer per process. A3-style failures (Quest and
    // Editor alone in their own "room A" instances) are most likely
    // explained by them landing on different Photon regions. Logging the
    // CloudRegion on both peers makes that observable in JSONL.
    private bool _regionLogged;

    // Photon event codes for the Relay-code handshake. Valid app range
    // is 1-199 (200+ reserved by Photon internals).
    //
    //   1 = broadcast — host pushes the code to a newly joined player
    //       via OnPlayerEnteredRoom (the "host hears your join, sends
    //       rc unprompted" path).
    //   2 = request — joiner pings the host on room entry asking for
    //       the code (covers the case where OnPlayerEnteredRoom on the
    //       host hasn't fired yet or otherwise misses us).
    //   3 = response — host's reply to a request, targeted at the
    //       requesting actor.
    //
    // Broadcast + request/response run in parallel. Whichever arrives
    // first on the joiner side wins; the late one is ignored because
    // the TCS is already completed.
    private const byte RelayCodeBroadcastEventCode = 1;
    private const byte RelayCodeRequestEventCode = 2;
    private const byte RelayCodeResponseEventCode = 3;

    // Host-side: when set, OnPlayerEnteredRoom forwards this code to the
    // newly-joined player via OpRaiseEvent. Cleared when the host stops.
    private string _hostRelayCodeForBroadcast;

    // Joiner-side: created at the start of WaitForRelayCodeEventAsync,
    // completed by OnEvent when the relay-code event arrives, or by
    // timeout. We track the captured instance locally to avoid
    // cross-call leaks if the user re-presses Join.
    private TaskCompletionSource<string> _relayCodeWaiter;

    public bool InRoom => _inRoom;
    public string CurrentRoomName => _voice?.Client?.CurrentRoom?.Name ?? "";

    // Axis A: "ready to operate" — i.e. ConnectedToMasterServer or already
    // Joined to a room. Both states accept OpJoinOrCreateRoom (Photon
    // auto-leaves an existing room on a new join). Disconnected,
    // Authenticating, ConnectingToNameServer etc. all return false.
    public bool IsConnectedToMaster
    {
        get
        {
            var s = _voice?.Client?.State;
            return s == ClientState.ConnectedToMasterServer || s == ClientState.Joined;
        }
    }

    public string CurrentState => _voice?.Client?.State.ToString() ?? "null";

    void Start()
    {
        _voice = GetComponent<VoiceConnection>();

        if (PhotonNetwork.PhotonServerSettings != null &&
            PhotonNetwork.PhotonServerSettings.AppSettings != null)
        {
            _voice.Settings = PhotonNetwork.PhotonServerSettings.AppSettings;
        }

        _voice.ConnectUsingSettings();
        _status = "Voice: connecting…";
        DebugLogger.Log("voice_connect_attempt");
    }

    void Update()
    {
        if (_voice == null || _voice.Client == null) return;

        var state = _voice.Client.State;

        // Register Photon callbacks once the client exists. Photon's
        // AddCallbackTarget needs a live Client; doing this in Start
        // could race against `_voice.ConnectUsingSettings()` if Photon
        // hasn't created the Client yet.
        if (!_callbacksRegistered)
        {
            _voice.Client.AddCallbackTarget(this);
            _callbacksRegistered = true;
        }

        // Only log on state transitions, never every frame.
        // Axis A: also use the transition edge to resolve the outcome of
        // any in-flight reconnect attempt — a clean transition to
        // ConnectedToMasterServer is success, a fall back to Disconnected
        // from a "connecting" state is failure.
        if (state != _lastLoggedState)
        {
            DebugLogger.Log("voice_state", null, ("state", state.ToString()));
            // Axis A diagnostic: log the Photon Voice region we landed on
            // once, when we first reach ConnectedToMasterServer per process.
            // Compare Editor + Quest logs to see if A3-style failures
            // (separate "room A" instances) line up with region mismatch.
            if (!_regionLogged && state == ClientState.ConnectedToMasterServer)
            {
                _regionLogged = true;
                DebugLogger.Log("voice_region_selected", null,
                    ("region", _voice.Client.CloudRegion ?? ""),
                    ("app_version", Application.version),
                    ("platform", Application.platform.ToString()));
            }
            if (_reconnectInFlight)
            {
                if (state == ClientState.ConnectedToMasterServer || state == ClientState.Joined)
                {
                    DebugLogger.Log("voice_reconnect_succeeded");
                    _reconnectInFlight = false;
                    _disconnectedWhileInRoom = false;
                }
                else if (state == ClientState.Disconnected &&
                         (_lastLoggedState == ClientState.ConnectingToMasterServer ||
                          _lastLoggedState == ClientState.Authenticating ||
                          _lastLoggedState == ClientState.ConnectingToNameServer ||
                          _lastLoggedState == ClientState.ConnectedToNameServer))
                {
                    DebugLogger.Log("voice_reconnect_failed", null,
                        ("from_state", _lastLoggedState.ToString()));
                    _reconnectInFlight = false;
                }
            }
            _lastLoggedState = state;
        }

        // Axis A: focus-regain reconnect trigger. Photon Voice sockets can
        // be reaped by the OS when the app is backgrounded (Quest puts the
        // app to sleep on focus loss). On focus regain, if the client is
        // disconnected, kick a single reconnect. We do NOT auto-rejoin the
        // room — that's the user's call (press host/join again).
        bool focusedNow = Application.isFocused;
        if (focusedNow && !_wasFocused && state == ClientState.Disconnected)
        {
            DebugLogger.Log("voice_focus_regain_reconnect", null,
                ("from_state", state.ToString()));
            TryReconnect();
        }
        _wasFocused = focusedNow;

        if (state == ClientState.ConnectedToMasterServer && !string.IsNullOrEmpty(_pendingRoom))
        {
            var name = _pendingRoom;
            _pendingRoom = null;
            DoJoinRoom(name);
            return;
        }

        if (state == ClientState.Joined)
        {
            if (!_inRoom) DebugLogger.Log("voice_joined", null, ("room", _voice.Client.CurrentRoom?.Name ?? ""));
            _inRoom = true;
            _status = $"Voice connected ({_voice.Client.CurrentRoom?.Name})";
        }
        else
        {
            if (_inRoom && state == ClientState.ConnectedToMasterServer)
            {
                _inRoom = false;
                _status = "Voice: left room";
                DebugLogger.Log("voice_left_room");
            }
            else if (state == ClientState.Disconnected)
            {
                if (_inRoom)
                {
                    DebugLogger.Log("voice_disconnected_while_in_room");
                    _disconnectedWhileInRoom = true;
                }
                _inRoom = false;
                _status = "Voice: disconnected";

                // Axis A: idle disconnect → schedule one reconnect after
                // a short backoff. Guarded by _reconnectScheduled so a
                // re-entrant Update can't queue duplicates, and by
                // Application.isFocused so a backgrounded app doesn't burn
                // battery retrying.
                if (_disconnectedWhileInRoom && !_reconnectScheduled && Application.isFocused)
                {
                    _reconnectScheduled = true;
                    _ = ScheduleIdleReconnectAsync();
                }
            }
            else if (!_inRoom)
            {
                _status = $"Voice: {state}";
            }
        }
    }

    // Single-shot delayed reconnect — runs once per disconnect-while-in-room
    // event, never loops, and only fires if the app is still focused at the
    // moment of the attempt. Cleared via _reconnectScheduled at completion
    // (success or failure observed via the next state transition).
    private async Task ScheduleIdleReconnectAsync()
    {
        await Task.Delay((int)(IdleReconnectBackoffSeconds * 1000));
        if (Application.isFocused && _voice?.Client?.State == ClientState.Disconnected)
        {
            DebugLogger.Log("voice_idle_disconnect_reconnect");
            TryReconnect();
        }
        _reconnectScheduled = false;
    }

    public void JoinRoom(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return;
        if (_voice == null || _voice.Client == null) { _pendingRoom = roomName; return; }

        if (_voice.Client.State == ClientState.ConnectedToMasterServer)
        {
            DoJoinRoom(roomName);
        }
        else
        {
            _pendingRoom = roomName;
            _status = $"Voice room joining… ({roomName})";
        }
    }

    void DoJoinRoom(string roomName)
    {
        var enterRoomParams = new EnterRoomParams
        {
            RoomName = roomName,
            RoomOptions = new RoomOptions { MaxPlayers = 4 },
        };
        _voice.Client.OpJoinOrCreateRoom(enterRoomParams);
        _status = $"Voice room joining… ({roomName})";
        DebugLogger.Log("voice_room_join_attempt", null, ("room", roomName));
    }

    public void LeaveRoom()
    {
        if (_voice == null || _voice.Client == null) return;
        if (_inRoom)
        {
            _voice.Client.OpLeaveRoom(false);
            DebugLogger.Log("voice_room_leave_requested");
        }
        _pendingRoom = null;
    }

    // Toggle outgoing voice transmission via the primary Recorder's
    // TransmitEnabled flag. Called by AppLifecycle when the Meta system
    // menu opens (focus lost) so mic audio captured locally is no longer
    // streamed to Photon's cloud servers; restored on focus regain.
    //
    // Mutes transmission rather than recording — the mic stream stays
    // initialised so audio resumes instantly when the user returns to
    // the app. RecordingEnabled-toggle would cause a sub-second gap on
    // resume due to mic re-initialisation.
    //
    // Returns true if the call reached the Recorder (whether or not a
    // toggle actually happened); false if voice/recorder isn't wired
    // yet — in which case nothing is being transmitted anyway, so the
    // caller can treat this as a successful no-op.
    public bool SetTransmitEnabled(bool enabled)
    {
        var rec = _voice?.PrimaryRecorder;
        if (rec == null) return false;
        if (rec.TransmitEnabled == enabled) return true;
        rec.TransmitEnabled = enabled;
        return true;
    }

    public bool IsTransmitting => _voice?.PrimaryRecorder?.TransmitEnabled ?? false;

    // Discovery for the Relay join code uses *player* custom properties
    // rather than room custom properties. Photon Voice's LoadBalancingClient
    // (verified empirically in the 2026-06-15 two-headset tests) does not
    // propagate room CustomProperties to new joiners even when the key is
    // listed in CustomRoomPropertiesForLobby and set at room creation
    // time. Player properties broadcast unconditionally to all room
    // members on join and on every update — the documented robust path
    // for this pattern in Photon Realtime.
    //
    // Host writes its own LocalPlayer property; joiner reads the master
    // client player's property (the host is always the master client
    // since OpJoinOrCreateRoom makes the first caller the master, and
    // host calls JoinRoom before any joiner does in our flow).

    public bool SetLocalPlayerProperty(string key, string value)
    {
        var lp = _voice?.Client?.LocalPlayer;
        if (lp == null) return false;
        var props = new Hashtable { { key, value } };
        return lp.SetCustomProperties(props);
    }

    public string GetLocalPlayerProperty(string key)
    {
        var lp = _voice?.Client?.LocalPlayer;
        if (lp == null) return null;
        if (lp.CustomProperties.TryGetValue(key, out object v)) return v as string;
        return null;
    }

    public string GetMasterClientPlayerProperty(string key)
    {
        var room = _voice?.Client?.CurrentRoom;
        if (room == null) return null;
        var master = room.GetPlayer(room.MasterClientId);
        if (master == null) return null;
        if (master.CustomProperties.TryGetValue(key, out object v)) return v as string;
        return null;
    }

    public async Task<string> WaitForMasterClientPlayerPropertyAsync(string key, float timeoutSeconds = 8f, int pollMs = 200)
    {
        float waited = 0f;
        while (waited < timeoutSeconds)
        {
            var v = GetMasterClientPlayerProperty(key);
            if (!string.IsNullOrEmpty(v)) return v;
            await Task.Delay(pollMs);
            waited += pollMs / 1000f;
        }
        return null;
    }

    public async Task<bool> WaitForRoomJoinedAsync(float timeoutSeconds = 8f, int pollMs = 200)
    {
        float waited = 0f;
        while (waited < timeoutSeconds)
        {
            if (_inRoom) return true;
            await Task.Delay(pollMs);
            waited += pollMs / 1000f;
        }
        return _inRoom;
    }

    // -------- Axis A: connection-state hardening ---------------------
    //
    // Axis A separates "is Photon Voice ready to operate" from "is the
    // Relay-code discovery wired" so failures don't tangle. NetworkBootstrap
    // calls WaitForConnectedAsync as a preflight before host/join.
    // TryReconnect is idempotent and never loops — one shot per trigger.

    public async Task<bool> WaitForConnectedAsync(float timeoutSeconds = 5f, int pollMs = 200)
    {
        float waited = 0f;
        while (waited < timeoutSeconds)
        {
            if (IsConnectedToMaster) return true;
            await Task.Delay(pollMs);
            waited += pollMs / 1000f;
        }
        return IsConnectedToMaster;
    }

    // Returns true if a reconnect is now in-flight (or we were already
    // connected). Returns false only if we can't even attempt — e.g. the
    // VoiceConnection component hasn't initialised yet.
    public bool TryReconnect()
    {
        if (_voice?.Client == null) return false;
        if (IsConnectedToMaster) return true;
        // Already in the middle of a connection attempt — don't queue a
        // second ConnectUsingSettings, Photon doesn't like that.
        var s = _voice.Client.State;
        if (s == ClientState.ConnectingToMasterServer ||
            s == ClientState.Authenticating ||
            s == ClientState.ConnectingToNameServer ||
            s == ClientState.ConnectedToNameServer) return true;

        DebugLogger.Log("voice_reconnect_attempt", null, ("from_state", s.ToString()));
        _reconnectInFlight = true;
        _voice.ConnectUsingSettings();
        return true;
    }

    // -------- Relay code discovery via Photon events (Plan C1) ----------
    //
    // Photon Voice's LoadBalancingClient does not propagate custom room
    // or player properties to new joiners (verified across three
    // 2026-06-15 headset tests: room-prop original, room-prop with
    // CustomRoomPropertiesForLobby + initial-create, master-client
    // player-prop). Photon *events* however do propagate — that's how
    // voice works. So we use OpRaiseEvent to hand the Relay code from
    // host to joiner whenever a player enters the host's room.
    //
    // The existing SetLocalPlayerProperty / GetLocalPlayerProperty flow
    // remains as a host-side diagnostic (confirms the host can write to
    // its own LocalPlayer custom properties — useful signal independent
    // of the broadcast path). Joiners no longer read player or room
    // properties for discovery.

    // Host stores the Relay code that should be forwarded to any new
    // joiner. Cleared on session teardown.
    public void PublishRelayCodeToJoiners(string relayCode)
    {
        _hostRelayCodeForBroadcast = relayCode;
    }

    public void ClearPublishedRelayCode()
    {
        _hostRelayCodeForBroadcast = null;
    }

    // Joiner-side: await the next relay-code event with a timeout.
    // Returns the code, or null on timeout. Safe under single-flight
    // re-presses (NetworkBootstrap's `_busy` guard ensures one caller
    // at a time).
    //
    // Also fires an immediate request event so the host can reply even
    // if its OnPlayerEnteredRoom callback timing is off — defensive
    // against Photon delivering player-join notifications after the
    // joiner has finished its `voice_joined` handling.
    public async Task<string> WaitForRelayCodeEventAsync(float timeoutSeconds = 8f)
    {
        var tcs = new TaskCompletionSource<string>();
        _relayCodeWaiter = tcs;

        // Ask the host for the code. Targeted at the master client
        // (= host in our flow). Failure to send is logged but not fatal —
        // OnPlayerEnteredRoom broadcast may still arrive.
        if (_voice?.Client != null && _voice.Client.InRoom)
        {
            var requestOptions = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
            bool sent = _voice.Client.OpRaiseEvent(
                RelayCodeRequestEventCode,
                null,
                requestOptions,
                SendOptions.SendReliable);
            DebugLogger.Log("relay_code_request_sent", null, ("queued", sent));
        }

        var timeoutTask = Task.Delay((int)(timeoutSeconds * 1000));
        var winner = await Task.WhenAny(tcs.Task, timeoutTask);

        // Detach so a late event doesn't accidentally fill the next
        // call's TCS with stale data.
        if (_relayCodeWaiter == tcs) _relayCodeWaiter = null;

        if (winner == tcs.Task) return await tcs.Task;
        DebugLogger.Log("relay_join_code_event_timeout", null,
            ("timeout_seconds", timeoutSeconds));
        return null;
    }

    // -------- IInRoomCallbacks ----------------------------------------

    // Host's response to a new player joining: forward the Relay code.
    // Targets only the new player (TargetActors). No-op if we're not
    // hosting / haven't published a code yet.
    public void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (string.IsNullOrEmpty(_hostRelayCodeForBroadcast)) return;
        if (_voice?.Client == null) return;

        var options = new RaiseEventOptions { TargetActors = new[] { newPlayer.ActorNumber } };
        bool sent = _voice.Client.OpRaiseEvent(
            RelayCodeBroadcastEventCode,
            _hostRelayCodeForBroadcast,
            options,
            SendOptions.SendReliable);

        DebugLogger.Log("relay_host_event_broadcast", null,
            ("target_actor", newPlayer.ActorNumber),
            ("queued", sent));
    }

    public void OnPlayerLeftRoom(Player otherPlayer) { /* no-op */ }
    public void OnMasterClientSwitched(Player newMasterClient) { /* no-op */ }
    public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { /* no-op */ }
    public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) { /* no-op */ }

    // -------- IOnEventCallback ----------------------------------------

    // Three event flows go through OnEvent. Other Photon Voice
    // internal events fall through.
    //
    // - Broadcast / Response (codes 1, 3): joiner consumes; carry the
    //   rc string. Whichever arrives first wins.
    // - Request (code 2): host consumes; replies with a Response
    //   targeted at the requesting actor.
    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case RelayCodeBroadcastEventCode:
            {
                if (!(photonEvent.CustomData is string code)) return;
                DebugLogger.Log("relay_code_broadcast_received", null,
                    ("code_length", code.Length));
                _relayCodeWaiter?.TrySetResult(code);
                return;
            }

            case RelayCodeResponseEventCode:
            {
                if (!(photonEvent.CustomData is string code)) return;
                DebugLogger.Log("relay_code_response_received", null,
                    ("code_length", code.Length));
                _relayCodeWaiter?.TrySetResult(code);
                return;
            }

            case RelayCodeRequestEventCode:
            {
                DebugLogger.Log("relay_code_request_received", null,
                    ("from_actor", photonEvent.Sender));
                if (string.IsNullOrEmpty(_hostRelayCodeForBroadcast)) return;
                if (_voice?.Client == null) return;

                var responseOptions = new RaiseEventOptions
                {
                    TargetActors = new[] { photonEvent.Sender },
                };
                bool sent = _voice.Client.OpRaiseEvent(
                    RelayCodeResponseEventCode,
                    _hostRelayCodeForBroadcast,
                    responseOptions,
                    SendOptions.SendReliable);
                DebugLogger.Log("relay_code_response_sent", null,
                    ("target_actor", photonEvent.Sender),
                    ("queued", sent));
                return;
            }
        }
    }

    void OnGUI()
    {
        if (!Application.isEditor) return;

        GUI.Label(new Rect(20, 380, 800, 28), _status);
    }
}
