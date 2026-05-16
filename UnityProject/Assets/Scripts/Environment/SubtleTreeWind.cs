using UnityEngine;

// Tiny per-frame rotation that makes a tree sway around its base.
// Designed to read as "calm forest breathing", not "trees at a rave".
//
// Cost: one Mathf.Sin + one Quaternion build + one transform.localRotation
// assignment per frame. Zero allocations. Negligible for a few dozen trees
// on Quest 3.
//
// Notes:
//   - Pivot is the Transform position. For the Mountain Terrain pack's
//     tree_01, the mesh origin sits at trunk base (Y=0 in the mesh's
//     local space), so a small rotation reads as a tilt — the top moves
//     more than the base. Exactly what we want.
//   - At amplitude 0.5° and a ~6 m tall tree, the top moves ~5 cm peak-
//     to-peak. Subtle on purpose. Bump amplitudeDegrees up if you want
//     a windier feel, but past ~2° it starts looking rubbery.
//   - phaseOffset is randomized on Start by default so a forest of
//     trees doesn't sway in lockstep.
//   - enableWind is a runtime toggle without needing to disable the
//     component (useful for A/B testing in Inspector at play time).
public class SubtleTreeWind : MonoBehaviour
{
    [SerializeField, Tooltip("Maximum sway angle in degrees. 0.3–1.0 = subtle; > 2 starts looking like a rubber stick.")]
    private float amplitudeDegrees = 0.5f;

    [SerializeField, Tooltip("Sway speed in radians/second. 0.4–0.7 reads as slow breathing.")]
    private float speed = 0.5f;

    [SerializeField, Tooltip("Per-tree phase offset in radians. Auto-randomized on Start unless randomizeOnStart is off.")]
    private float phaseOffset = 0f;

    [SerializeField, Tooltip("Axis to sway around, in local space. Vector3.right (X) gives a forward/back tilt.")]
    private Vector3 axis = Vector3.right;

    [SerializeField, Tooltip("Randomize phaseOffset on Start so trees don't sway in unison.")]
    private bool randomizeOnStart = true;

    [SerializeField, Tooltip("Runtime toggle — flip to freeze the tree mid-motion without losing state.")]
    private bool enableWind = true;

    private Quaternion _baseRotation;

    void Awake()
    {
        _baseRotation = transform.localRotation;
    }

    void Start()
    {
        if (randomizeOnStart) phaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        if (!enableWind) return;
        float angle = Mathf.Sin(Time.time * speed + phaseOffset) * amplitudeDegrees;
        transform.localRotation = _baseRotation * Quaternion.AngleAxis(angle, axis);
    }
}
