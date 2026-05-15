using Photon.Realtime;
using Photon.Voice.Unity;
using UnityEngine;

[RequireComponent(typeof(VoiceConnection))]
public class VoiceBootstrap : MonoBehaviour
{
    private VoiceConnection _voice;
    private string _status = "Voice: idle";

    void Start()
    {
        _voice = GetComponent<VoiceConnection>();
        if (_voice.Client != null) _voice.Client.StateChanged += OnClientStateChanged;
        _voice.ConnectUsingSettings();
        _status = "Voice: connecting…";
    }

    void OnDestroy()
    {
        if (_voice != null && _voice.Client != null)
            _voice.Client.StateChanged -= OnClientStateChanged;
    }

    private void OnClientStateChanged(ClientState from, ClientState to)
    {
        _status = $"Voice: {to}";
    }

    void OnGUI()
    {
        GUI.Label(new Rect(20, 380, 800, 28), _status);
    }
}
