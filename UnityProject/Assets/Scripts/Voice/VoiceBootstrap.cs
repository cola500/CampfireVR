using System.Threading.Tasks;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.Unity;
using UnityEngine;

[RequireComponent(typeof(VoiceConnection))]
public class VoiceBootstrap : MonoBehaviour
{
    private VoiceConnection _voice;
    private string _status = "Voice: idle";
    private string _pendingRoom;
    private bool _inRoom;
    private ClientState _lastLoggedState = ClientState.PeerCreated;

    public bool InRoom => _inRoom;
    public string CurrentRoomName => _voice?.Client?.CurrentRoom?.Name ?? "";

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

        // Only log on state transitions, never every frame.
        if (state != _lastLoggedState)
        {
            DebugLogger.Log("voice_state", null, ("state", state.ToString()));
            _lastLoggedState = state;
        }

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
                if (_inRoom) DebugLogger.Log("voice_disconnected_while_in_room");
                _inRoom = false;
                _status = "Voice: disconnected";
            }
            else if (!_inRoom)
            {
                _status = $"Voice: {state}";
            }
        }
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

    public bool SetRoomProperty(string key, string value)
    {
        var room = _voice?.Client?.CurrentRoom;
        if (room == null) return false;
        var props = new Hashtable { { key, value } };
        return room.SetCustomProperties(props);
    }

    public string GetRoomProperty(string key)
    {
        var room = _voice?.Client?.CurrentRoom;
        if (room == null) return null;
        if (room.CustomProperties.TryGetValue(key, out object v)) return v as string;
        return null;
    }

    public async Task<string> WaitForRoomPropertyAsync(string key, float timeoutSeconds = 5f, int pollMs = 200)
    {
        float waited = 0f;
        while (waited < timeoutSeconds)
        {
            var v = GetRoomProperty(key);
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

    void OnGUI()
    {
        if (!Application.isEditor) return;

        GUI.Label(new Rect(20, 380, 800, 28), _status);
    }
}
