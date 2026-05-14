using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class XRDebugLogger : MonoBehaviour
{
    [SerializeField] private float delaySeconds = 1f;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(delaySeconds);

        var rig = transform;
        var cameraOffset = transform.Find("CameraOffset");
        var cam = cameraOffset != null ? cameraOffset.Find("VRCamera") : null;

        Debug.Log($"[XRDebug] VRRig world pos: {rig.position}  euler: {rig.eulerAngles}");
        if (cameraOffset != null)
            Debug.Log($"[XRDebug] CameraOffset local: {cameraOffset.localPosition}  world: {cameraOffset.position}");
        if (cam != null)
            Debug.Log($"[XRDebug] VRCamera local: {cam.localPosition}  world: {cam.position}");

        var subs = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count == 0) Debug.Log("[XRDebug] No XRInputSubsystems found.");
        foreach (var s in subs)
        {
            Debug.Log($"[XRDebug] XRInputSubsystem running={s.running}  origin={s.GetTrackingOriginMode()}  supported={s.GetSupportedTrackingOriginModes()}");
        }
    }
}
