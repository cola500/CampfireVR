using UnityEngine;

// Centralised handler for Unity's application-lifecycle callbacks
// (focus / pause / resume). Boots before scene load like DebugLogger,
// runs as a DontDestroyOnLoad singleton, and routes lifecycle events
// to the debug log + the voice mic mute.
//
// What this slice deliberately does:
// - Logs app_focus_lost / app_focus_gained / app_paused / app_resumed
//   on every transition (with voice-mute state for context).
// - Mutes Photon Voice transmission when focus is lost (mic still
//   captures locally but nothing reaches Photon's cloud). Restores
//   transmission when focus comes back. Mutes via Recorder.TransmitEnabled
//   instead of disconnect-on-pause so the user's room + relay session
//   stay intact across a system-menu open / close.
//
// What this slice deliberately does NOT do:
// - Touch NGO heartbeats, Relay session, or NetworkBootstrap state.
//   Existing connections survive a brief focus loss; tearing them down
//   would force the user (and their friend) to re-join. The long-press-Y
//   Stop flow remains the explicit recovery path.
// - Freeze XRHeadTracker pose writes. Remote players will see the local
//   player's head "frozen" at its last-tracked position during a menu
//   open — that's the better UX than a head that keeps drifting from the
//   pose Quest reports while the runtime has redirected to the menu.
//   A future slice can wire AppLifecycle.IsFocused to XRHeadTracker if we
//   observe specific issues during headset testing.
public class AppLifecycle : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("AppLifecycle");
        DontDestroyOnLoad(go);
        go.AddComponent<AppLifecycle>();
    }

    public bool IsFocused => _hasFocus;
    public bool IsPaused => _isPaused;

    private VoiceBootstrap _voice;
    private bool _hasFocus = true;
    private bool _isPaused;
    // Tracks whether *this* component muted the voice transmission, so
    // we don't accidentally unmute on focus regain if voice was muted
    // for some other reason in the future.
    private bool _mutedByUs;

    void Start()
    {
        // Single lookup at Start. Both AppLifecycle and VoiceBootstrap
        // exist as scene objects at the time Start runs; order doesn't
        // matter for FindFirstObjectByType.
        _voice = FindAnyObjectByType<VoiceBootstrap>();
        DebugLogger.Log("app_lifecycle_ready", null,
            ("voice_bootstrap_found", _voice != null));
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus == _hasFocus) return;
        _hasFocus = hasFocus;

        if (hasFocus)
        {
            bool unmuted = ApplyVoiceMute(false);
            DebugLogger.Log("app_focus_gained", null,
                ("voice_transmit_restored", unmuted));
        }
        else
        {
            bool muted = ApplyVoiceMute(true);
            DebugLogger.Log("app_focus_lost", null,
                ("voice_transmit_muted", muted));
        }
    }

    void OnApplicationPause(bool isPaused)
    {
        if (isPaused == _isPaused) return;
        _isPaused = isPaused;
        // Focus and pause typically fire together on Quest when the
        // system menu opens, so the voice mute is already handled by
        // OnApplicationFocus. Pause gets its own log line for clarity —
        // useful when diagnosing logs after a session.
        DebugLogger.Log(isPaused ? "app_paused" : "app_resumed", null,
            ("voice_muted_by_us", _mutedByUs));
    }

    // Returns true if a real toggle happened; false if voice wasn't
    // available, was already in the desired state, or the call failed.
    bool ApplyVoiceMute(bool mute)
    {
        if (_voice == null) return false;

        if (mute)
        {
            if (_mutedByUs) return false; // already muted by us
            bool ok = _voice.SetTransmitEnabled(false);
            if (ok) _mutedByUs = true;
            return ok;
        }
        else
        {
            if (!_mutedByUs) return false; // wasn't muted by us; leave alone
            bool ok = _voice.SetTransmitEnabled(true);
            if (ok) _mutedByUs = false;
            return ok;
        }
    }
}
