using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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

    private string _status = "Idle";
    private string _joinCodeInput = "";
    private bool _prevA, _prevB;
    private ServicesBootstrap _services;
    private bool _busy;

    void Awake()
    {
        _services = GetComponent<ServicesBootstrap>();
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

        var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (right.isValid)
        {
            right.TryGetFeatureValue(CommonUsages.primaryButton, out bool a);
            right.TryGetFeatureValue(CommonUsages.secondaryButton, out bool b);
            if (a && !_prevA) StartHost();
            if (b && !_prevB) StartClient();
            _prevA = a; _prevB = b;
        }
    }

    void ToggleMode()
    {
        mode = (mode == Mode.Lan) ? Mode.Relay : Mode.Lan;
        _status = $"Mode: {mode}";
    }

    async void StartHost()
    {
        if (_busy) return;
        if (mode == Mode.Lan)
        {
            if (!ConfigureLanTransport()) return;
            _status = NetworkManager.Singleton.StartHost() ? $"LAN HOST on :{port}" : "LAN host failed";
        }
        else
        {
            if (_services == null || !_services.IsReady) { _status = "Auth not ready"; return; }
            _busy = true;
            _status = "Creating Relay session…";
            var code = await _services.HostRelayAsync();
            _busy = false;
            _status = code != null ? $"RELAY HOST  code: {code}" : "Relay host failed";
        }
    }

    async void StartClient()
    {
        if (_busy) return;
        if (mode == Mode.Lan)
        {
            if (!ConfigureLanTransport()) return;
            _status = NetworkManager.Singleton.StartClient() ? $"LAN CLIENT → {serverAddress}:{port}" : "LAN client failed";
        }
        else
        {
            if (_services == null || !_services.IsReady) { _status = "Auth not ready"; return; }
            if (string.IsNullOrEmpty(_joinCodeInput)) { _status = "Enter join code"; return; }
            _busy = true;
            _status = $"Joining {_joinCodeInput}…";
            bool ok = await _services.JoinRelayAsync(_joinCodeInput);
            _busy = false;
            _status = ok ? $"RELAY CLIENT → {_joinCodeInput}" : "Relay join failed";
        }
    }

    async void Stop()
    {
        if (_services != null && _services.InRelaySession)
            await _services.LeaveRelayAsync();
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
            NetworkManager.Singleton.Shutdown();
        _status = "Stopped";
    }

    bool ConfigureLanTransport()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) { _status = "No NetworkManager"; return false; }
        var t = nm.GetComponent<UnityTransport>();
        if (t == null) { _status = "No UnityTransport"; return false; }
        t.SetConnectionData(serverAddress, port, "0.0.0.0");
        return true;
    }

    void OnGUI()
    {
        GUI.Label(new Rect(20, 20, 800, 30), $"Net: {_status}    (H=Host, C=Client, X=Stop, M=Mode  |  A=Host, B=Client)");
        GUI.Label(new Rect(20, 50, 800, 30), $"Local IPs: {string.Join(", ", GetLocalIPv4())}");
        GUI.Label(new Rect(20, 80, 800, 30), $"Mode: {mode}");

        if (mode == Mode.Relay)
        {
            GUI.Label(new Rect(20, 170, 200, 30), "Join code:");
            _joinCodeInput = GUI.TextField(new Rect(120, 170, 200, 30), _joinCodeInput ?? "", 8);
            if (_services != null && _services.InRelaySession && !string.IsNullOrEmpty(_services.JoinCode))
                GUI.Label(new Rect(20, 200, 800, 40), $"YOUR JOIN CODE: {_services.JoinCode}");
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
