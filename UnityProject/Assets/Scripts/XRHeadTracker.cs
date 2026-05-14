using UnityEngine;
using UnityEngine.XR;

public class XRHeadTracker : MonoBehaviour
{
    [SerializeField] private XRNode node = XRNode.CenterEye;

    void OnEnable() { Application.onBeforeRender += UpdatePose; }
    void OnDisable() { Application.onBeforeRender -= UpdatePose; }
    void Update() { UpdatePose(); }

    void UpdatePose()
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return;
        if (device.TryGetFeatureValue(CommonUsages.devicePosition, out var pos))
            transform.localPosition = pos;
        if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rot))
            transform.localRotation = rot;
    }
}
