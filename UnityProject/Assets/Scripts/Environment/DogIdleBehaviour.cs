using UnityEngine;

// Subtle idle life for the campfire dog. Three effects layered on
// top of whatever the Animator is doing, all authoritative on the
// root transform; the rotational ones compose additively so they
// can happen at the same time without fighting each other:
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
// 3. Attention glance — a slightly larger Y-rotation (~8°, clamped),
//    every 15–45 s with a 50 % chance of firing each interval. Aims
//    toward the local VR camera (Camera.main) so the dog occasionally
//    appears to notice the player. Clamped so it can never lock-on
//    or stare; on the next interval it returns to baseline. This is
//    *local-camera attention*, not real voice-activity-driven
//    speaker targeting — see docs/dog-idle-behavior-slice.md for the
//    intended future evolution.
//
// Why the existing Animator's idle isn't enough: the vendor's
// Dog_001_idle.anim has `m_LoopTime: 0`, so the BlendTree's "idle"
// motion plays once on spawn and freezes at the final pose. We don't
// modify the vendor asset (CLAUDE.md "never move/rename files inside
// vendor folders") — we layer subtle life on the root instead.
//
// Deliberately NOT in this script: bone-level animation, Animator
// parameter writes, NavMesh, audio, IK, networking, voice-activity
// detection, speech recognition, audio analysis. The dog stays
// fully local to each headset.
//
// Performance: one Update with a Sin + two envelopes + at most one
// Camera.main lookup per glance trigger. Well under 0.05 ms on
// Quest 3. Stays in the perf budget identified in
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

    [Header("Attention glance — occasional, toward player camera")]
    [SerializeField, Tooltip("Master switch — leave off to disable the glance entirely.")]
    private bool enableAttentionGlance = true;

    [SerializeField, Range(0f, 60f), Tooltip("Min seconds between glance attempts.")]
    private float minGlanceInterval = 15f;

    [SerializeField, Range(0f, 120f), Tooltip("Max seconds between glance attempts.")]
    private float maxGlanceInterval = 45f;

    [SerializeField, Range(0f, 1f), Tooltip("Chance the dog actually glances when the interval fires (rest of the time it stays still — feels less mechanical).")]
    private float glanceProbability = 0.5f;

    [SerializeField, Range(0.5f, 8f), Tooltip("How long a glance lasts (look out + hold + look back).")]
    private float glanceDuration = 3f;

    [SerializeField, Range(0f, 30f), Tooltip("Max yaw deflection in degrees. Clamped, so even if the player is behind the dog, the glance is just a small turn.")]
    private float glanceMaxYaw = 8f;

    [SerializeField, Tooltip("Look-at target. If null, falls back to Camera.main (= the local VR camera in a normal scene) at glance time. Drag a Transform here if you want the dog to look at something else.")]
    private Transform glanceTarget;

    private Vector3 _baselineLocalPos;
    private Quaternion _baselineLocalRot;
    private float _phase;
    private float _nextShiftAt;
    private float _shiftDir = 1f;
    private float _shiftStartTime = -1f;
    private float _nextGlanceAt;
    private float _glanceStartTime = -1f;
    private float _glanceYawAngle;

    void OnEnable()
    {
        _baselineLocalPos = transform.localPosition;
        _baselineLocalRot = transform.localRotation;
        _phase = Random.value * Mathf.PI * 2f;
        ScheduleNextShift();
        ScheduleNextGlance();
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

        // Attention glance — third layer, composes additively with the
        // weight shift on the Y axis. Total yaw stays small even when
        // both fire at once (worst case: shift ±2° + glance ±8° = ±10°).
        float glanceAngle = enableAttentionGlance ? TickGlance(t) : 0f;

        transform.localPosition = _baselineLocalPos + new Vector3(0f, bob, 0f);
        transform.localRotation = _baselineLocalRot * Quaternion.Euler(0f, shiftAngle + glanceAngle, 0f);
    }

    // Returns the current glance contribution in degrees, or 0 if no
    // glance is active. Also drives the schedule machine — fires the
    // glance probabilistically when the interval lapses, expires the
    // glance after `glanceDuration`.
    private float TickGlance(float t)
    {
        if (_glanceStartTime >= 0f)
        {
            float elapsed = t - _glanceStartTime;
            if (elapsed >= glanceDuration)
            {
                _glanceStartTime = -1f;
                ScheduleNextGlance();
                return 0f;
            }
            return SampleProportionalEnvelope(elapsed, glanceDuration) * _glanceYawAngle;
        }
        if (t >= _nextGlanceAt)
        {
            // Reschedule first so a probability-skip still pushes the
            // next attempt forward — otherwise we'd retry next frame.
            ScheduleNextGlance();
            if (Random.value > glanceProbability) return 0f;

            float yawToTarget = ComputeYawToTarget();
            if (float.IsNaN(yawToTarget)) return 0f;
            // Skip if we're already facing close enough — saves an
            // animation cycle that would produce no visible motion.
            if (Mathf.Abs(yawToTarget) < 0.5f) return 0f;

            _glanceYawAngle = Mathf.Clamp(yawToTarget, -glanceMaxYaw, glanceMaxYaw);
            _glanceStartTime = t;
            DebugLogger.Log("dog_attention_glance", null,
                ("yaw_degrees", System.Math.Round(_glanceYawAngle, 1)),
                ("target", glanceTarget != null ? glanceTarget.name : "Camera.main"));
        }
        return 0f;
    }

    // World yaw from dog's baseline orientation toward the resolved
    // target. Returns NaN if no target can be resolved or the target
    // is essentially on top of the dog. Computes baseline through the
    // parent so a future parent move wouldn't break the angle.
    private float ComputeYawToTarget()
    {
        Transform target = glanceTarget != null
            ? glanceTarget
            : (Camera.main != null ? Camera.main.transform : null);
        if (target == null) return float.NaN;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.01f) return float.NaN;

        float worldYawToTarget = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
        Quaternion baselineWorld = (transform.parent != null ? transform.parent.rotation : Quaternion.identity) * _baselineLocalRot;
        float baselineWorldYaw = baselineWorld.eulerAngles.y;
        return Mathf.DeltaAngle(baselineWorldYaw, worldYawToTarget);
    }

    private void ScheduleNextGlance()
    {
        float min = Mathf.Min(minGlanceInterval, maxGlanceInterval);
        float max = Mathf.Max(minGlanceInterval, maxGlanceInterval);
        _nextGlanceAt = Time.time + Random.Range(min, max);
    }

    // Lerp 30 % out → hold 40 % → lerp 30 % back, eased with SmoothStep.
    // Different shape from the weight-shift envelope (which uses
    // absolute seconds) so the two can have distinct feels even if
    // they fire at the same time.
    private float SampleProportionalEnvelope(float elapsed, float total)
    {
        float lerpDuration = total * 0.3f;
        if (elapsed < lerpDuration)
            return Mathf.SmoothStep(0f, 1f, elapsed / lerpDuration);
        elapsed -= lerpDuration;
        float holdDuration = total * 0.4f;
        if (elapsed < holdDuration) return 1f;
        elapsed -= holdDuration;
        return Mathf.SmoothStep(1f, 0f, elapsed / Mathf.Max(lerpDuration, 0.01f));
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
