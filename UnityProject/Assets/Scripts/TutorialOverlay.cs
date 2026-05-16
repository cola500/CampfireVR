using System.Text;
using UnityEngine;

public class TutorialOverlay : MonoBehaviour
{
    [SerializeField] private TextMesh text;
    [SerializeField] private float billboardSmoothing = 8f;
    [SerializeField] private float notificationSeconds = 5f;

    private const string UniversalLegend =
        "X  host\n" +
        "B  join / next / confirm\n" +
        "Y  back / mode\n" +
        "A  change / recenter";

    private NetworkBootstrap _net;
    private Camera _cam;
    private string _lastSeenState = "";
    private float _notificationUntil;

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

        if (_net == null) { text.text = ""; return; }

        string state = _net.CurrentState ?? "";
        if (state != _lastSeenState)
        {
            _lastSeenState = state;
            if (!string.IsNullOrEmpty(state)) _notificationUntil = Time.time + notificationSeconds;
        }
        bool showNotification = Time.time < _notificationUntil;
        string busySpinner = _net.IsBusy ? Spinner() : "";
        string notification = showNotification ? state + busySpinner : "";

        switch (_net.CurrentPhase)
        {
            case NetworkBootstrap.Phase.Joining:
            {
                int last = _net.CodeLengthSlots - 1;
                bool onLast = _net.CodeSlot >= last;
                text.text =
                    "🔥  JOIN FIRE\n" +
                    "\n" +
                    _net.CodeDisplay + "\n" +
                    "\n" +
                    (string.IsNullOrEmpty(notification) ? "" : notification + "\n\n") +
                    "stick  change letter\n" +
                    (onLast ? "B      join\n" : "B      next slot\n") +
                    "Y      back";
                break;
            }

            case NetworkBootstrap.Phase.Hosting:
            {
                string body;
                if (!string.IsNullOrEmpty(_net.HostedAlias))
                {
                    body =
                        "share code\n" +
                        SpaceLetters(_net.HostedAlias) + "\n" +
                        "\n" +
                        (string.IsNullOrEmpty(notification) ? "waiting for friend" + Spinner() : notification);
                }
                else
                {
                    body = string.IsNullOrEmpty(notification)
                        ? "Creating fire" + Spinner()
                        : notification;
                }
                text.text =
                    "🔥  YOUR FIRE\n" +
                    "\n" +
                    body + "\n" +
                    "\n" +
                    UniversalLegend;
                break;
            }

            case NetworkBootstrap.Phase.Connecting:
            {
                string body = string.IsNullOrEmpty(notification)
                    ? "Joining fire" + Spinner()
                    : notification;
                text.text =
                    "🔥  CAMPFIRE\n" +
                    "\n" +
                    body + "\n" +
                    "\n" +
                    UniversalLegend;
                break;
            }

            case NetworkBootstrap.Phase.Connected:
                text.text = showNotification ? "🔥  " + state : "";
                break;

            case NetworkBootstrap.Phase.Idle:
            default:
            {
                string modeLine = $"mode · {_net.CurrentModeLabel}";
                text.text =
                    "🔥  CAMPFIRE\n" +
                    "\n" +
                    UniversalLegend + "\n" +
                    "\n" +
                    modeLine +
                    (string.IsNullOrEmpty(notification) ? "" : "\n" + notification);
                break;
            }
        }
    }

    static string Spinner()
    {
        int n = (int)(Time.time * 2.5f) % 4;
        switch (n)
        {
            case 1: return " .";
            case 2: return " . .";
            case 3: return " . . .";
            default: return "";
        }
    }

    static string SpaceLetters(string code)
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
}
