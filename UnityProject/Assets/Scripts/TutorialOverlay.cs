using Unity.Netcode;
using UnityEngine;

public class TutorialOverlay : MonoBehaviour
{
    [SerializeField] private TextMesh text;
    [SerializeField] private float billboardSmoothing = 8f;

    private NetworkBootstrap _net;
    private Camera _cam;

    void Awake()
    {
        if (text == null) text = GetComponentInChildren<TextMesh>();
    }

    void Start()
    {
        _net = FindFirstObjectByType<NetworkBootstrap>();
    }

    void LateUpdate()
    {
        if (text == null) return;

        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
        {
            var toCam = transform.position - _cam.transform.position;
            if (toCam.sqrMagnitude > 0.0001f)
            {
                var target = Quaternion.LookRotation(toCam, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, billboardSmoothing * Time.deltaTime);
            }
        }

        var nm = NetworkManager.Singleton;
        bool connected = nm != null && (nm.IsHost || nm.IsClient);

        string mode  = _net != null ? _net.CurrentMode.ToString() : "";
        string state = _net != null ? _net.CurrentState           : "";

        if (connected)
        {
            text.text = state;
        }
        else if (_net != null && _net.IsEditingCode)
        {
            int lastSlot = _net.CodeLengthSlots - 1;
            bool onLast = _net.CodeSlot >= lastSlot;
            text.text =
                $"Mode: {mode}\n" +
                "\n" +
                "JOIN CODE  (A B C)\n" +
                _net.CodeDisplay + "\n" +
                "\n" +
                $"SLOT:  {_net.CodeSlot + 1} / {_net.CodeLengthSlots}\n" +
                $"VALUE: {_net.CodeValue}\n" +
                "\n" +
                "A = NEXT LETTER     (hold to auto-cycle)\n" +
                "X = PREVIOUS LETTER (hold to auto-cycle)\n" +
                (onLast ? "B = JOIN\n" : "B = NEXT SLOT\n") +
                "Y = BACK\n" +
                "\n" +
                $"LAST INPUT:  {(string.IsNullOrEmpty(_net.LastButton) ? "—" : _net.LastButton)}\n" +
                $"LAST ACTION: {(string.IsNullOrEmpty(_net.LastAction) ? "—" : _net.LastAction)}\n" +
                $"CTRL: L={(_net.LeftHandValid ? "ok" : "off")}  R={(_net.RightHandValid ? "ok" : "off")}\n" +
                "\n" +
                state;
        }
        else
        {
            text.text =
                $"Mode: {mode}\n" +
                "\n" +
                "PRESS X TO HOST\n" +
                "PRESS B TO JOIN\n" +
                "PRESS Y TO SWITCH MODE\n" +
                "PRESS A TO RECENTER\n" +
                "\n" +
                $"LAST INPUT:  {(_net == null || string.IsNullOrEmpty(_net.LastButton) ? "—" : _net.LastButton)}\n" +
                $"LAST ACTION: {(_net == null || string.IsNullOrEmpty(_net.LastAction) ? "—" : _net.LastAction)}\n" +
                $"CTRL: L={(_net != null && _net.LeftHandValid ? "ok" : "off")}  R={(_net != null && _net.RightHandValid ? "ok" : "off")}\n" +
                "\n" +
                state;
        }
    }
}
