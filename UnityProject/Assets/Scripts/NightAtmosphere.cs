using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class NightAtmosphere : MonoBehaviour
{
    [SerializeField] private Color ambientColor = new Color(0.03f, 0.04f, 0.08f, 1f);
    [SerializeField] private Material skybox;

    void OnEnable()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    void Apply()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.skybox = skybox; // null = camera SolidColor background takes over
    }
}
