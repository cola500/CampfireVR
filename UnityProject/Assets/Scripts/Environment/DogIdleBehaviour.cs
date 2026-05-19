using UnityEngine;

// Subtle idle life for the campfire dog. Two effects layered on top
// of whatever the Animator is doing, both authoritative on the root
// transform:
//
// 1. Breathing — a small sinusoidal Y-position offset (~5 mm at the
//    default 0.5× scale), period ~4 s. Reads as a sleeping dog's
//    belly rising and falling. Continuous.
//
// 2. Weight shift — a small Y-rotation offset (~2°), every 12–25 s
//    (uniform random), lerping out + holding briefly + lerping back.
//    Reads as the dog turning its snout slightly to track the fire's
//    flicker. Occasional, never repeats on a fixed beat.
//
// Why the existing Animator's idle isn't enough: the vendor's
// Dog_001_idle.anim has `m_LoopTime: 0`, so the BlendTree's "idle"
// motion plays once on spawn and freezes at the final pose. We don't
// modify the vendor asset (CLAUDE.md "never move/rename files inside
// vendor folders") — we layer subtle life on the root instead.
//
// Deliberately NOT in this script: bone-level animation, Animator
// parameter writes, NavMesh, audio, IK, networking. The dog stays
// fully local to each headset.
//
// Performance: one Update with a Sin + a Lerp = well under 0.01 ms
// on Quest 3. Stays in the perf budget identified in
// docs/performance-checklist.md.
//
// Style mirrors Scripts/Environment/PresenceBreath.cs — same project
// pattern of [SerializeField] knobs + baseline-captured-in-OnEnable +
// phase randomisation for desynchronisation across multiple instances.
public class DogIdleBehaviour : MonoBehaviour
{
    [Header("Breathing — continuous")]
    [SerializeField, Range(0f, 0.05f), Tooltip("Vertical amplitude in metres (local space).")]
    private float breathingAmplitude = 0.005f;

    [SerializeField, Range(1f, 10f), Tooltip("Seconds per full inhale-exhale cycle.")]
    private float breathingPeriod = 4f;

    [Header("Weight shift — occasional")]
    [SerializeField, Range(0f, 10f), Tooltip("Rotation amplitude in degrees (Y axis).")]
    private float weightShiftAngle = 2f;

    [SerializeField, Tooltip("Min seconds between weight shifts.")]
    private float weightShiftMinInterval = 12f;

    [SerializeField, Tooltip("Max seconds between weight shifts.")]
    private float weightShiftMaxInterval = 25f;

    [SerializeField, Range(0.2f, 3f), Tooltip("Seconds to lerp from neutral to peak.")]
    private float weightShiftLerpSeconds = 0.7f;

    [SerializeField, Range(0f, 3f), Tooltip("Seconds to hold at peak before returning.")]
    private float weightShiftHoldSeconds = 0.4f;

    private Vector3 _baselineLocalPos;
    private Quaternion _baselineLocalRot;
    private float _phase;
    private float _nextShiftAt;
    private float _shiftDir = 1f;
    private float _shiftStartTime = -1f;

    void OnEnable()
    {
        _baselineLocalPos = transform.localPosition;
        _baselineLocalRot = transform.localRotation;
        _phase = Random.value * Mathf.PI * 2f;
        ScheduleNextShift();
    }

    void Update()
    {
        float t = Time.time;

        // Breathing — continuous sine, additive to the captured baseline.
        float bob = 0f;
        if (breathingPeriod > 0.01f)
        {
            float omega = Mathf.PI * 2f / breathingPeriod;
            bob = Mathf.Sin(t * omega + _phase) * breathingAmplitude;
        }

        // Weight shift — fires occasionally with a lerp-hold-lerp envelope.
        float shiftAngle = 0f;
        if (_shiftStartTime >= 0f)
        {
            float elapsed = t - _shiftStartTime;
            float total = weightShiftLerpSeconds * 2f + weightShiftHoldSeconds;
            if (elapsed >= total)
            {
                _shiftStartTime = -1f;
                ScheduleNextShift();
            }
            else
            {
                shiftAngle = SampleShiftEnvelope(elapsed) * weightShiftAngle * _shiftDir;
            }
        }
        else if (t >= _nextShiftAt)
        {
            _shiftStartTime = t;
            // Coin-flip direction so the dog doesn't always tilt the same way.
            _shiftDir = Random.value < 0.5f ? -1f : 1f;
        }

        transform.localPosition = _baselineLocalPos + new Vector3(0f, bob, 0f);
        transform.localRotation = _baselineLocalRot * Quaternion.Euler(0f, shiftAngle, 0f);
    }

    // Returns 0 → 1 → 0 across the lerp-hold-lerp envelope; clamped to 0
    // outside it (caller already guards the bounds).
    private float SampleShiftEnvelope(float elapsed)
    {
        if (elapsed < weightShiftLerpSeconds)
            return Mathf.SmoothStep(0f, 1f, elapsed / weightShiftLerpSeconds);
        elapsed -= weightShiftLerpSeconds;
        if (elapsed < weightShiftHoldSeconds)
            return 1f;
        elapsed -= weightShiftHoldSeconds;
        return Mathf.SmoothStep(1f, 0f, elapsed / Mathf.Max(weightShiftLerpSeconds, 0.01f));
    }

    private void ScheduleNextShift()
    {
        float min = Mathf.Min(weightShiftMinInterval, weightShiftMaxInterval);
        float max = Mathf.Max(weightShiftMinInterval, weightShiftMaxInterval);
        _nextShiftAt = Time.time + Random.Range(min, max);
    }
}
