using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Voice pool and mixer for every sound in the game.
///
/// Gameplay code never touches an AudioSource: it hands a SoundEvent and, for
/// world sounds, a position, and this decides whether that sound is allowed
/// right now and which voice plays it. Centralising that is what keeps a busy
/// moment - a field ripening while the stove finishes and a truck pulls in -
/// from turning into clipped mush, via the per-event rate limit and a hard cap
/// on live voices.
///
/// The entry points are STATIC and silent when no manager is in the scene, so
/// gameplay can announce sounds without caring whether the audio layer exists -
/// the same contract RunStats uses for its counters. That matters here more than
/// it did in the project this came from: the farm has scenes and prefabs that get
/// exercised without a full GameManager rig, and a missing manager should cost a
/// sound, never a NullReferenceException.
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("Voices")]
    [Tooltip("Simultaneous sounds. Once they're all busy the oldest is recycled, so " +
             "a burst of new events always beats whatever is already fading out.")]
    [SerializeField] int voiceCount = 24;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] float ambienceVolume = 0.3f;

    [Header("World space")]
    // The listener rides the camera, which follows the player at a fixed 28 units
    // (CameraFollow.distance), so ANY sound the player is standing next to is already
    // 28 units from the listener. Unity's default logarithmic rolloff is full volume
    // at 1 unit and inverse-square after, which would make even the plot underfoot
    // near-silent. The linear window below is sized to the farm instead: full volume
    // out to just past the camera's own distance, fading to nothing well beyond the
    // far side of the island.
    [Tooltip("Distance at which a world sound is still at full volume. Should sit just " +
             "outside the camera's follow distance, or sounds at the player's feet are " +
             "already attenuated.")]
    [SerializeField] float fullVolumeDistance = 30f;
    [Tooltip("Distance at which a world sound reaches silence. Must clear the camera's " +
             "distance to the far corner of the fully expanded island, or that corner " +
             "goes mute.")]
    [SerializeField] float silenceDistance = 90f;
    [Tooltip("How positional world sounds are. Fully 3D reads as thin and far away " +
             "from this camera height; a little 2D keeps them present in the mix while " +
             "still panning left/right with the action.")]
    [SerializeField, Range(0f, 1f)] float worldSpatialBlend = 0.75f;

    [Header("Ambience")]
    [Tooltip("Looping bed under everything else - birdsong, wind. Optional.")]
    [SerializeField] AudioClip ambienceLoop;

    [Header("Debug")]
    [Tooltip("Log every sound that actually plays. Rate-limited requests are not " +
             "logged, so the console shows what was heard rather than what was asked " +
             "for. Worth switching off once a sound has been signed off.")]
    [SerializeField] bool logPlays;

    public static AudioManager Instance { get; private set; }

    // Player-set levels live here rather than in the settings UI, so audio starts at
    // the saved level on the very first frame instead of at whatever the scene was
    // authored with. The serialized fields double as the defaults: an unset key
    // falls back to what the AudioManager already carries.
    const string SfxVolumeKey = "audio.sfxVolume";
    const string AmbienceVolumeKey = "audio.ambienceVolume";

    AudioSource[] _voices;
    float[] _voiceStartedAt;
    AudioSource _ambienceSource;

    /// <summary>
    /// Last time each event was allowed through, for the per-event rate limit. Kept
    /// here rather than on the SoundEvent asset because assets outlive a play
    /// session - a timestamp written into one would still be there on the next run.
    /// </summary>
    readonly Dictionary<SoundEvent, float> _lastPlayedAt = new Dictionary<SoundEvent, float>();

    /// <summary>Current SFX level, 0-1. Safe to read with no manager in the scene.</summary>
    public static float SfxVolume => Instance != null ? Instance.sfxVolume : 0f;

    /// <summary>Current ambience level, 0-1. Safe to read with no manager in the scene.</summary>
    public static float AmbienceVolume => Instance != null ? Instance.ambienceVolume : 0f;

    /// <summary>
    /// Claims the static instance, restores saved levels, then builds the voice pool
    /// and starts the ambience bed - in that order, so the bed starts at the
    /// player's own volume rather than the scene's authored one.
    /// </summary>
    void Awake()
    {
        Instance = this;
        LoadVolumePrefs();
        BuildVoices();
        StartAmbience();
    }

    /// <summary>
    /// Releases the static instance, but only if this manager still owns it - a
    /// scene reload builds the new one before tearing the old one down, and clearing
    /// unconditionally would leave the live manager unreachable.
    /// </summary>
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Reads the saved levels, falling back to whatever the component was authored
    /// with so the serialized fields double as the defaults.
    /// </summary>
    void LoadVolumePrefs()
    {
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume));
        ambienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(AmbienceVolumeKey, ambienceVolume));
    }

    /// <summary>
    /// Sets the SFX level immediately. Sounds already playing keep the level they
    /// started at - they are all short, and re-scaling live voices would make a drag
    /// of the slider audibly lurch.
    /// </summary>
    public static void SetSfxVolume(float value)
    {
        if (Instance == null) return;
        Instance.sfxVolume = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Sets the ambience level immediately. Unlike SFX this DOES re-scale the live
    /// source, because the bed is one long loop - if it only took effect on the next
    /// play, the slider would appear to do nothing.
    /// </summary>
    public static void SetAmbienceVolume(float value)
    {
        if (Instance == null) return;

        Instance.ambienceVolume = Mathf.Clamp01(value);
        if (Instance._ambienceSource != null)
            Instance._ambienceSource.volume = Instance.ambienceVolume * Instance.masterVolume;
    }

    /// <summary>
    /// Writes the current levels to PlayerPrefs. Called when the settings dialog
    /// closes rather than on every slider tick, which would hit the disk on each
    /// pixel of a drag.
    /// </summary>
    public static void SaveVolumePrefs()
    {
        if (Instance == null) return;

        PlayerPrefs.SetFloat(SfxVolumeKey, Instance.sfxVolume);
        PlayerPrefs.SetFloat(AmbienceVolumeKey, Instance.ambienceVolume);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Creates the fixed pool of AudioSources every sound plays through, configured
    /// for the farm-sized linear rolloff described on the fields above.
    /// </summary>
    void BuildVoices()
    {
        int count = Mathf.Max(1, voiceCount);
        _voices = new AudioSource[count];
        _voiceStartedAt = new float[count];

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Voice " + i);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            // Linear rather than the default logarithmic: see the field comments above.
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = fullVolumeDistance;
            source.maxDistance = silenceDistance;

            _voices[i] = source;
            _voiceStartedAt[i] = float.NegativeInfinity;
        }
    }

    /// <summary>
    /// Starts the looping ambience bed, if one is authored. Flat stereo on purpose:
    /// a bed that panned with the action would draw attention to itself.
    /// </summary>
    void StartAmbience()
    {
        if (ambienceLoop == null) return;

        var go = new GameObject("Ambience");
        go.transform.SetParent(transform, false);

        _ambienceSource = go.AddComponent<AudioSource>();
        _ambienceSource.clip = ambienceLoop;
        _ambienceSource.loop = true;
        _ambienceSource.playOnAwake = false;
        // Flat stereo: a bed that pans as the action moves would draw attention to
        // itself, which is the opposite of what a bed is for.
        _ambienceSource.spatialBlend = 0f;
        _ambienceSource.volume = ambienceVolume * masterVolume;
        _ambienceSource.Play();
    }

    /// <summary>Plays a sound at a point on the farm. Silent if no manager exists.</summary>
    public static void PlayAt(SoundEvent sound, Vector3 position)
    {
        if (Instance != null) Instance.Play(sound, position, true);
    }

    /// <summary>
    /// Plays a sound flat, with no position - menu clicks, the level-up flourish,
    /// anything the player should hear identically wherever it happened on the farm.
    /// Silent if no manager exists.
    /// </summary>
    public static void PlayUI(SoundEvent sound)
    {
        if (Instance != null) Instance.Play(sound, Vector3.zero, false);
    }

    /// <summary>
    /// Central play routine: rate-limits, picks a take, acquires a voice and sets
    /// its level, pitch and spatial blend. Every entry point lands here.
    /// </summary>
    void Play(SoundEvent sound, Vector3 position, bool positional)
    {
        if (sound == null || !sound.HasClips || !PassesRateLimit(sound)) return;

        AudioClip clip = sound.PickClip();
        if (clip == null) return;

        AudioSource voice = AcquireVoice();
        voice.transform.position = position;
        voice.clip = clip;
        voice.volume = sound.Volume * sfxVolume * masterVolume;
        voice.pitch = sound.RandomPitch;

        float authored = sound.SpatialBlendOverride;
        voice.spatialBlend = !positional ? 0f
                           : authored >= 0f ? authored
                           : worldSpatialBlend;

        voice.Play();

        if (logPlays)
        {
            // The clip is named as well as the event: an event with several takes
            // would otherwise give the same line whichever one was picked, which is
            // the one thing you want to see when checking a sound's variation.
            Debug.Log(positional
                ? $"[SFX] {sound.name} ({clip.name}) @ {position}"
                : $"[SFX] {sound.name} ({clip.name}) [UI]");
        }
    }

    /// <summary>
    /// True when enough time has passed since this event last played. Uses unscaled
    /// time because the game freezes timeScale on the win screen, and the sounds
    /// around that moment still have to be heard.
    /// </summary>
    bool PassesRateLimit(SoundEvent sound)
    {
        float interval = sound.MinRetriggerInterval;
        if (interval <= 0f) return true;

        float now = Time.unscaledTime;
        if (_lastPlayedAt.TryGetValue(sound, out float previous) && now - previous < interval)
            return false;

        _lastPlayedAt[sound] = now;
        return true;
    }

    /// <summary>
    /// A free voice, or the longest-running one when they're all busy. Stealing the
    /// oldest means a new event always lands - what it interrupts is whatever is
    /// furthest into its tail, and so closest to inaudible already.
    /// </summary>
    AudioSource AcquireVoice()
    {
        int oldestIndex = 0;
        float oldestTime = float.PositiveInfinity;

        for (int i = 0; i < _voices.Length; i++)
        {
            if (!_voices[i].isPlaying)
            {
                _voiceStartedAt[i] = Time.unscaledTime;
                return _voices[i];
            }

            if (_voiceStartedAt[i] < oldestTime)
            {
                oldestTime = _voiceStartedAt[i];
                oldestIndex = i;
            }
        }

        _voiceStartedAt[oldestIndex] = Time.unscaledTime;
        return _voices[oldestIndex];
    }
}
