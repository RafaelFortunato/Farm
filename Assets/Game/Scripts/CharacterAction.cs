using UnityEngine;

/// <summary>
/// One scripted beat the player performs: planting, harvesting, cooking, upgrading a
/// building. Input is locked for the duration and the character plays whatever this
/// asset describes.
///
/// Authored as an asset so a new beat needs data, not code. Once a clip exists, fill in
/// animatorTrigger and the Animator drives the pose; until then the procedural
/// crouch/lean below stands in for it.
/// </summary>
[CreateAssetMenu(fileName = "Action_", menuName = "Farm/Character Action")]
public class CharacterAction : ScriptableObject
{
    [Tooltip("How long input stays locked, in seconds.")]
    public float duration = 1f;

    [Tooltip("Turn to face the object being used before the beat starts.")]
    public bool faceTarget = true;

    [Header("Animation")]
    [Tooltip("Animator trigger fired when the beat starts. Leave empty to use the procedural pose below.")]
    public string animatorTrigger;

    [Tooltip("The clip the trigger plays. Only used to time-stretch it onto the duration above.")]
    public AnimationClip clip;

    [Header("Procedural pose (used only when no trigger is set)")]
    [Tooltip("How far the character dips down at the middle of the beat, in world units.")]
    public float crouch = 0.3f;
    [Tooltip("How far the character leans forward at the middle of the beat, in degrees.")]
    public float lean = 22f;

    [Header("Camera framing")]
    [Tooltip("How far the camera pushes in during the beat, in world units. 0 leaves it alone.")]
    public float cameraZoom;
    [Tooltip("Extra downward tilt during the beat, in degrees. Positive looks further down.")]
    public float cameraPitch;
    [Tooltip("Seconds the camera takes to ease in, and to ease back out afterwards.")]
    public float cameraBlend = 0.35f;

    /// <summary>True while no clip is authored, so the stand-in pose should play.</summary>
    public bool UsesProceduralPose => string.IsNullOrEmpty(animatorTrigger);

    /// <summary>
    /// Animator speed that makes the clip fill exactly `duration`, so the input lock and
    /// the animation can never drift apart - tune the beat by changing duration alone.
    /// Falls back to 1 when there is no clip to measure.
    /// </summary>
    public float PlaybackSpeed =>
        clip != null && duration > 0.0001f ? clip.length / duration : 1f;

    /// <summary>Cached so starting a beat does no string hashing.</summary>
    public int TriggerHash { get; private set; }

    void OnEnable() => CacheTrigger();
    void OnValidate() => CacheTrigger();

    void CacheTrigger() => TriggerHash = UsesProceduralPose ? 0 : Animator.StringToHash(animatorTrigger);
}
