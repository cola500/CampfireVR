using Unity.Netcode;
using UnityEngine;

public class NetworkHead : NetworkBehaviour
{
    [SerializeField] private GameObject visual;

    private Transform _camera;
    private Transform _ownRig;
    private Transform _remoteRig;
    private Quaternion _rotDiff;

    private MeshRenderer[] _placeholderRenderers;
    private bool _placeholderHidden;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            if (visual != null) visual.SetActive(false);

            var cam = Camera.main;
            if (cam != null) _camera = cam.transform;

            _ownRig    = FindInactiveByName("VRRig")?.transform;
            _remoteRig = FindInactiveByName("RemoteRig")?.transform;

            if (_ownRig != null && _remoteRig != null)
                _rotDiff = _remoteRig.rotation * Quaternion.Inverse(_ownRig.rotation);
        }
        else
        {
            var placeholder = FindInactiveByName("PlayerSlot_B");
            if (placeholder != null)
            {
                _placeholderRenderers = placeholder.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var r in _placeholderRenderers) r.enabled = false;
                _placeholderHidden = true;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_placeholderHidden && _placeholderRenderers != null)
        {
            foreach (var r in _placeholderRenderers) if (r != null) r.enabled = true;
            _placeholderHidden = false;
        }
    }

    void LateUpdate()
    {
        if (!IsOwner || _camera == null || _ownRig == null || _remoteRig == null) return;

        Vector3 offset = _camera.position - _ownRig.position;
        transform.position = _remoteRig.position + _rotDiff * offset;
        transform.rotation = _rotDiff * _camera.rotation;
    }

    private static GameObject FindInactiveByName(string n)
    {
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in all)
            if (t.name == n) return t.gameObject;
        return null;
    }
}
