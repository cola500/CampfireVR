using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class XRTrackingOriginSetter : MonoBehaviour
{
    [SerializeField] private TrackingOriginModeFlags mode = TrackingOriginModeFlags.Floor;
    [SerializeField] private bool recenterOnStart = true;

    void Start()
    {
        var subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var s in subsystems)
        {
            s.TrySetTrackingOriginMode(mode);
            if (recenterOnStart) s.TryRecenter();
        }
    }
}
