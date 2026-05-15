using Unity.Netcode;
using UnityEngine;

public class NetworkHead : NetworkBehaviour
{
    [SerializeField] private GameObject visual;
    [SerializeField] private GameObject visualLeft;
    [SerializeField] private GameObject visualRight;

    private readonly NetworkVariable<Vector3> _leftPos = new(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<Quaternion> _leftRot = new(
        Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<Vector3> _rightPos = new(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<Quaternion> _rightRot = new(
        Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Transform _camera;
    private Transform _ownRig;
    private Transform _remoteRig;
    private Transform _leftSource;
    private Transform _rightSource;
    private Quaternion _rotDiff;

    private MeshRenderer[] _placeholderRenderers;
    private bool _placeholderHidden;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            if (visual != null) visual.SetActive(false);
            if (visualLeft != null) visualLeft.SetActive(false);
            if (visualRight != null) visualRight.SetActive(false);

            var cam = Camera.main;
            if (cam != null) _camera = cam.transform;

            _ownRig      = FindInactiveByName("VRRig")?.transform;
            _remoteRig   = FindInactiveByName("RemoteRig")?.transform;
            _leftSource  = FindInactiveByName("LeftHandAnchor")?.transform;
            _rightSource = FindInactiveByName("RightHandAnchor")?.transform;

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
        if (IsOwner)
        {
            if (_camera == null || _ownRig == null || _remoteRig == null) return;

            Vector3 headOffset = _camera.position - _ownRig.position;
            transform.position = _remoteRig.position + _rotDiff * headOffset;
            transform.rotation = _rotDiff * _camera.rotation;

            if (_leftSource != null)
            {
                Vector3 o = _leftSource.position - _ownRig.position;
                _leftPos.Value = _remoteRig.position + _rotDiff * o;
                _leftRot.Value = _rotDiff * _leftSource.rotation;
            }
            if (_rightSource != null)
            {
                Vector3 o = _rightSource.position - _ownRig.position;
                _rightPos.Value = _remoteRig.position + _rotDiff * o;
                _rightRot.Value = _rotDiff * _rightSource.rotation;
            }
        }
        else
        {
            if (visualLeft != null)
                visualLeft.transform.SetPositionAndRotation(_leftPos.Value, _leftRot.Value);
            if (visualRight != null)
                visualRight.transform.SetPositionAndRotation(_rightPos.Value, _rightRot.Value);
        }
    }

    private static GameObject FindInactiveByName(string n)
    {
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in all)
            if (t.name == n) return t.gameObject;
        return null;
    }
}
