using Photon.Pun;
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

        // VoiceConnection.ConnectUsingSettings reads from its own .Settings field,
        // not from PhotonServerSettings. Copy the AppSettings the user pasted into
        // the PUN wizard so a single source of config keeps working.
        if (PhotonNetwork.PhotonServerSettings != null &&
            PhotonNetwork.PhotonServerSettings.AppSettings != null)
        {
            _voice.Settings = PhotonNetwork.PhotonServerSettings.AppSettings;
        }

        _voice.ConnectUsingSettings();
        _status = "Voice: connecting…";
    }

    void Update()
    {
        if (_voice != null && _voice.Client != null)
            _status = $"Voice: {_voice.Client.State}";
    }

    void OnGUI()
    {
        GUI.Label(new Rect(20, 380, 800, 28), _status);
    }
}
