using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class ServicesBootstrap : MonoBehaviour
{
    public bool IsReady { get; private set; }
    public string PlayerId { get; private set; } = "";
    public string JoinCode => _session?.Code ?? "";
    public bool InRelaySession => _session != null;

    private ISession _session;
    private string _status = "Unity Services: initializing…";

    async void Start()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            PlayerId = AuthenticationService.Instance.PlayerId;
            IsReady = true;
            _status = "Unity Services: signed in";
        }
        catch (System.Exception e)
        {
            _status = $"Unity Services error: {e.Message}";
            Debug.LogError($"[ServicesBootstrap] {e}");
        }
    }

    public async Task<string> HostRelayAsync(int maxPlayers = 2)
    {
        try
        {
            var options = new SessionOptions
            {
                Name = "campfire",
                MaxPlayers = maxPlayers,
            }.WithRelayNetwork();
            _session = await MultiplayerService.Instance.CreateSessionAsync(options);
            return _session.Code;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ServicesBootstrap] HostRelay: {e}");
            return null;
        }
    }

    public async Task<bool> JoinRelayAsync(string code)
    {
        try
        {
            _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ServicesBootstrap] JoinRelay: {e}");
            return false;
        }
    }

    public async Task LeaveRelayAsync()
    {
        if (_session != null)
        {
            try { await _session.LeaveAsync(); } catch { /* swallow on shutdown */ }
            _session = null;
        }
    }

    void OnGUI()
    {
        GUI.Label(new Rect(20, 110, 800, 30), _status);
        if (!string.IsNullOrEmpty(PlayerId))
            GUI.Label(new Rect(20, 140, 800, 30), $"PlayerId: {PlayerId}");
    }
}
