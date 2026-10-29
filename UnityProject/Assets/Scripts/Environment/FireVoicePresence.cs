using UnityEngine;

// Makes the campfire feel a little more alive while the local player is
// talking. Reuses two existing systems and adds no new networking, no new
// assets, no particle-system changes:
//   - VoiceBootstrap.IsTransmitting (local mic only, already public)
//   - FireLightFlicker.IntensityMultiplier / FlickerMultiplier (added
//     alongside this slice as a hook — default 1, no behaviour change
//     unless this script is present)
//
// The effect must stay below the threshold of conscious notice. See
// docs/campfire-reacts-to-voice.md for the reasoning and target values.
public class FireVoicePresence : MonoBehaviour
{
    [SerializeField, Tooltip("Fire light intensity multiplier while talking. 1.05-1.10 = subtle, per spec.")]
    private float talkingIntensityMultiplier = 1.08f;

    [SerializeField, Tooltip("Flicker amplitude multiplier while talking. Same range as intensity.")]
    private float talkingFlickerMultiplier = 1.08f;

    [SerializeField, Tooltip("Seconds to ramp fully toward (or away from) the talking state. Linear — never a fast pulse.")]
    private float rampSeconds = 1.5f;

    [SerializeField, Tooltip("Log fire_reacts_to_voice on state-change edges. Never per-frame.")]
    private bool logStateChanges = true;

    [SerializeField] private FireLightFlicker fireLightFlicker;
    [SerializeField] private VoiceBootstrap voiceBootstrap;

    private float _presence; // 0 = idle, 1 = fully "talking"
    private bool _lastLoggedTalking;

    void OnEnable()
    {
        if (fireLightFlicker == null)
            fireLightFlicker = FindFirstObjectByType<FireLightFlicker>();
        if (voiceBootstrap == null)
            voiceBootstrap = FindFirstObjectByType<VoiceBootstrap>();

        _presence = 0f;
        _lastLoggedTalking = false;
    }

    void Update()
    {
        if (fireLightFlicker == null || voiceBootstrap == null) return;

        bool talking = voiceBootstrap.IsTransmitting;

        if (rampSeconds > 0f)
        {
            float target = talking ? 1f : 0f;
            _presence = Mathf.MoveTowards(_presence, target, Time.deltaTime / rampSeconds);
        }
        else
        {
            _presence = talking ? 1f : 0f;
        }

        fireLightFlicker.IntensityMultiplier = Mathf.Lerp(1f, talkingIntensityMultiplier, _presence);
        fireLightFlicker.FlickerMultiplier = Mathf.Lerp(1f, talkingFlickerMultiplier, _presence);

        if (logStateChanges && talking != _lastLoggedTalking)
        {
            _lastLoggedTalking = talking;
            DebugLogger.Log("fire_reacts_to_voice", null, ("talking", talking));
        }
    }
}
