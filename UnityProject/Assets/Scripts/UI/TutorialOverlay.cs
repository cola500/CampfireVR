using UnityEngine;

public class TutorialOverlay : MonoBehaviour
{
    [SerializeField] private TextMesh text;
    [SerializeField] private float billboardSmoothing = 8f;
    [SerializeField] private float notificationSeconds = 5f;

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

        char letter = _net.CurrentLetter;

        switch (_net.CurrentPhase)
        {
            case NetworkBootstrap.Phase.Hosting:
            {
                string body;
                if (!string.IsNullOrEmpty(_net.HostedAlias))
                {
                    body =
                        $"Room: {_net.HostedAlias}\n" +
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
                    body;
                break;
            }

            case NetworkBootstrap.Phase.Connecting:
            {
                string body = string.IsNullOrEmpty(notification)
                    ? $"Joining room {letter}" + Spinner()
                    : notification;
                text.text =
                    "🔥  CAMPFIRE\n" +
                    "\n" +
                    body;
                break;
            }

            case NetworkBootstrap.Phase.Connected:
                text.text = showNotification ? "🔥  " + state : "";
                break;

            case NetworkBootstrap.Phase.Idle:
            default:
            {
                text.text =
                    "🔥  CAMPFIRE\n" +
                    $"Room: {letter}\n" +
                    "\n" +
                    BuildLegend(letter) +
                    (string.IsNullOrEmpty(notification) ? "" : "\n\n" + notification);
                break;
            }
        }
    }

    static string BuildLegend(char letter) =>
        $"X       host room {letter}\n" +
        $"B       join room {letter}\n" +
        "A       recenter\n" +
        "stick   change room";

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
}
