using UnityEngine;

public class VoiceSpeakerPlacer : MonoBehaviour
{
    [SerializeField] private string remoteRigName = "RemoteRig";
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.2f, 0f);

    void OnEnable()
    {
        var rig = GameObject.Find(remoteRigName);
        if (rig == null) return;
        transform.SetParent(rig.transform, false);
        transform.localPosition = localOffset;
        transform.localRotation = Quaternion.identity;
    }
}
