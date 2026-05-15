using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class ServicesBootstrap : MonoBehaviour
{
    public bool IsReady { get; private set; }
    public string PlayerId { get; private set; } = "";

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

    void OnGUI()
    {
        GUI.Label(new Rect(20, 110, 800, 30), _status);
        if (!string.IsNullOrEmpty(PlayerId))
            GUI.Label(new Rect(20, 140, 800, 30), $"PlayerId: {PlayerId}");
    }
}
