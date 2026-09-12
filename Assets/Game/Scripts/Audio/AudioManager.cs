using System.Collections;
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
[DefaultExecutionOrder(-100)]
public class AudioManager : Singleton<AudioManager>
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
    // These are measured from the PLAYER: the AudioListener lives on the Player object, not on
    // the camera where Unity puts it by default. That matters more than it sounds. The camera
    // rides a fixed orbit about 21 units from the cat, so with the ear up there Unity never
    // measured less than 21 even with the player standing on the stove - and these numbers
    // were 30 and 90 to compensate, which left the whole walkable island inside the flat top
    // of the curve where nothing ever got quieter. With the ear on the cat the distance Unity
    // measures is the distance the player would say it is, so the window can be the honest
    // size of the farm. Individual sounds override both radii; see SoundEvent.
    //
    // The listener hangs off the player as a child called Listener. A Rotation Constraint on
    // that object sources the camera, so it keeps the fixed camera facing instead of spinning
    // with the cat - panning has to follow the screen, not the character. That is a stock
    // component with no script behind it, deliberately: it costs no per-frame managed call.
    [Tooltip("How close counts as being right there. Inside this a world sound plays at full " +
             "level, so it covers roughly the reach of an interaction - stand at the stove and " +
             "you hear the stove.")]
    [SerializeField] float fullVolumeDistance = 6f;
    [Tooltip("How far away a world sound fades out completely. Short enough that the far side " +
             "of the island is genuinely quiet, which is the point of positioning a sound at " +
             "all; long enough to cover what fits on screen at once.")]
    [SerializeField] float silenceDistance = 40f;
    [Tooltip("How positional world sounds are. 1 is fully 3D and falls to true silence; below " +
             "that a fraction of the sound stays flat stereo and never fades, which keeps a " +
             "distant event present in the mix rather than lost. A sound can override this.")]
    [SerializeField, Range(0f, 1f)] float worldSpatialBlend = 0.75f;

    [Header("Ambience")]
    [Tooltip("Looping bed under everything else - birdsong, wind. Optional.")]
    [SerializeField] AudioClip ambienceLoop;

    // Sounds that are the SAME everywhere they happen. A button click is a button click
    // whichever panel it is on, so a field per panel would be four references to one asset and
    // four chances to leave one empty. Sounds that belong to a particular thing - the stove, the
    // sell counter - stay on that thing, where they can be swapped without affecting anything
    // else.
    [Header("Shared cues")]
    [Tooltip("Any plain button press.")]
    [SerializeField] SoundEvent uiClick;
    [Tooltip("A panel appearing.")]
    [SerializeField] SoundEvent uiOpen;
    [Tooltip("A panel dismissed.")]
    [SerializeField] SoundEvent uiClose;
    [Tooltip("An action the player asked for and cannot have - too few coins, too few crops.")]
    [SerializeField] SoundEvent uiDenied;
    [Tooltip("Anything entering the crate. Fires once per item, so it is authored quiet.")]
    [SerializeField] SoundEvent pickup;

    /// <summary>A plain button press.</summary>
    public static void PlayClick() => PlayUI(Instance != null ? Instance.uiClick : null);

    /// <summary>A panel appearing.</summary>
    public static void PlayOpen() => PlayUI(Instance != null ? Instance.uiOpen : null);

    /// <summary>A panel dismissed.</summary>
    public static void PlayClose() => PlayUI(Instance != null ? Instance.uiClose : null);

    /// <summary>An action the player cannot afford or is otherwise refused.</summary>
    public static void PlayDenied() => PlayUI(Instance != null ? Instance.uiDenied : null);

    /// <summary>Something landed in the crate, at the spot it came from.</summary>
    public static void PlayPickup(Vector3 position)
        => PlayAt(Instance != null ? Instance.pickup : null, position);

    // ---- loops ------------------------------------------------------------------
    //
    // Loops get their own voices rather than borrowing from the pool. The pool steals its
    // oldest voice when everything is busy, which is right for one-shots - the thing it
    // interrupts is nearly finished anyway - but would cut a running loop dead halfway
    // through a cook. There are only ever a handful of these, so they are made on demand
    // and kept, keyed by whatever owns them.
    //
    // The sound is kept alongside the source, not just the source. A loop outlives the call
    // that started it, so it is the only voice whose authored distances still need to be
    // known minutes later - when the asset is edited mid-cook, or when the manager's own
    // defaults are dragged in the inspector.
    class LoopVoice
    {
        public AudioSource Source;
        public SoundEvent Sound;
    }

    readonly Dictionary<Transform, LoopVoice> _loops = new Dictionary<Transform, LoopVoice>();

    /// <summary>
    /// Start a sound looping at <paramref name="owner"/>, or do nothing if that owner is
    /// already looping this one. Silent with no manager in the scene, like everything here.
    ///
    /// The owner is the key as well as the position, so a caller never has to hold a handle
    /// and cannot leak one - it starts and stops with the same reference it already has.
    /// </summary>
    public static void StartLoop(SoundEvent sound, Transform owner)
    {
        if (Instance == null || sound == null || owner == null || !sound.HasClips) return;
        Instance.BeginLoop(sound, owner);
    }

    /// <summary>Stop whatever <paramref name="owner"/> had looping. Safe to call twice.</summary>
    public static void StopLoop(Transform owner)
    {
        if (Instance == null || owner == null) return;
        Instance.EndLoop(owner);
    }

    void BeginLoop(SoundEvent sound, Transform owner)
    {
        var clip = sound.PickClip();
        if (clip == null) return;

        if (_loops.TryGetValue(owner, out var entry) && entry.Source != null)
        {
            if (entry.Source.clip == clip && entry.Source.isPlaying) return;   // already running
            entry.Source.Stop();
        }
        else
        {
            var go = new GameObject("Loop");
            go.transform.SetParent(transform, false);
            var created = go.AddComponent<AudioSource>();
            created.playOnAwake = false;
            entry = new LoopVoice { Source = created };
            _loops[owner] = entry;
        }

        entry.Sound = sound;

        var voice = entry.Source;
        voice.name = "Loop " + sound.name;
        voice.transform.position = owner.position;
        voice.clip = clip;
        voice.loop = true;
        voice.volume = sound.Volume * sfxVolume * masterVolume;
        voice.pitch = sound.RandomPitch;
        ApplyRolloff(voice, sound);

        float authored = sound.SpatialBlendOverride;
        voice.spatialBlend = authored >= 0f ? authored : worldSpatialBlend;
        voice.Play();

        if (logPlays) Debug.Log($"[SFX] {sound.name} looping at {owner.name}");
    }

    void EndLoop(Transform owner)
    {
        if (!_loops.TryGetValue(owner, out var entry)) return;

        if (entry.Source != null) { entry.Source.Stop(); entry.Source.clip = null; }
        _loops.Remove(owner);
    }

    /// <summary>
    /// Re-reads a sound's distances onto any loop currently playing it. Called by SoundEvent
    /// when its fields are edited, so tuning the stove's reach is audible while the stove is
    /// running rather than on the next cook. Null-safe and silent with no manager in the scene.
    /// </summary>
    public static void RefreshRolloff(SoundEvent sound)
    {
        if (Instance == null || sound == null) return;

        foreach (var entry in Instance._loops.Values)
            if (entry.Source != null && entry.Sound == sound)
                Instance.ApplyRolloff(entry.Source, sound);
    }

    [Header("Debug")]
    [Tooltip("Log every sound that actually plays. Rate-limited requests are not " +
             "logged, so the console shows what was heard rather than what was asked " +
             "for. Worth switching off once a sound has been signed off.")]
    [SerializeField] bool logPlays;

    // Player-set levels live here rather than in the settings UI, so audio starts at
    // the saved level on the very first frame instead of at whatever the scene was
    // authored with. The serialized fields double as the defaults: an unset key
    // falls back to what the AudioManager already carries.
    const string SfxVolumeKey = "audio.sfxVolume";
    const string AmbienceVolumeKey = "audio.ambienceVolume";

    AudioSource[] _voices;
    float[] _voiceStartedAt;
    AudioSource _ambienceSource;

    /// <summary>Temporary multiplier on the ambience bed, 1 when nothing is ducking it.</summary>
    float _ambienceDuck = 1f;
    Coroutine _duckRoutine;


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
    /// Restores saved levels, then builds the voice pool and starts the ambience bed - in that
    /// order, so the bed starts at the player's own volume rather than the scene's authored one.
    ///
    /// Runs from OnBind rather than Awake because Singleton binds in Awake AND OnEnable: a
    /// domain reload mid-play re-runs only OnEnable, so anything done solely in Awake is lost
    /// for the rest of the session. The guard is what makes a second bind harmless - the voices
    /// are already parented here, and building again would stack a second pool on top.
    /// </summary>
    protected override void OnBind()
    {
        if (_voices != null) return;

        LoadVolumePrefs();
        BuildVoices();
        StartAmbience();
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
        Instance.ApplyAmbienceVolume();
    }

    /// <summary>
    /// What the bed actually plays at: the player's setting, times whatever the duck is
    /// currently holding it down to.
    ///
    /// Kept separate so a duck never touches the SETTING. The two would otherwise fight -
    /// duck while the settings panel is open and the slider would jump, or worse, closing
    /// the panel would save the ducked level as the player's choice.
    /// </summary>
    void ApplyAmbienceVolume()
    {
        if (_ambienceSource != null)
            _ambienceSource.volume = ambienceVolume * masterVolume * _ambienceDuck;
    }

    /// <summary>
    /// Pulls the ambience bed down for a moment so something else can be heard over it,
    /// then eases it back. Used by the victory beat, where a five second fanfare would
    /// otherwise fight the music underneath it.
    ///
    /// Unscaled time throughout: the win screen freezes the clock, and a duck that never
    /// lifted because timeScale was zero would leave the game permanently quiet.
    ///
    /// A second call replaces the first rather than stacking, so a duck cannot be left
    /// half-applied by two overlapping beats.
    /// </summary>
    public static void DuckAmbience(float toFraction, float holdSeconds, float fadeSeconds = 0.8f)
    {
        if (Instance == null) return;

        if (Instance._duckRoutine != null) Instance.StopCoroutine(Instance._duckRoutine);
        Instance._duckRoutine = Instance.StartCoroutine(
            Instance.DuckRoutine(Mathf.Clamp01(toFraction), holdSeconds, Mathf.Max(fadeSeconds, 0.01f)));
    }

    IEnumerator DuckRoutine(float to, float hold, float fade)
    {
        // down fast, so the fanfare is clear from its first note
        float from = _ambienceDuck;
        for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
        {
            _ambienceDuck = Mathf.Lerp(from, to, t / 0.15f);
            ApplyAmbienceVolume();
            yield return null;
        }
        _ambienceDuck = to;
        ApplyAmbienceVolume();

        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

        // back up slowly, so the music returning is not itself an event
        for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
        {
            _ambienceDuck = Mathf.Lerp(to, 1f, t / fade);
            ApplyAmbienceVolume();
            yield return null;
        }
        _ambienceDuck = 1f;
        ApplyAmbienceVolume();
        _duckRoutine = null;
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
    /// How every world voice fades with distance. One method rather than the same four lines at
    /// each place a source is made, so the pool and the loop voices cannot drift apart - they
    /// did, and a distance change that only reached half the sources is not a thing you can hear
    /// your way to.
    ///
    /// Linear rather than Unity's default logarithmic. Logarithmic is inverse-square from one
    /// unit out, which is how sound behaves outdoors and completely wrong for a farm you look
    /// down on: the near field would collapse to nothing within a few steps. Linear spends the
    /// whole window on the distances the player actually walks.
    /// </summary>
    void ApplyRolloff(AudioSource source, SoundEvent sound)
    {
        source.rolloffMode = AudioRolloffMode.Linear;

        // The sound gets the last word, the manager supplies the default. Each radius is
        // decided on its own, so a sound can shorten only its tail and leave the near field
        // alone - which is the common case, and the one the stove wants.
        float near = fullVolumeDistance;
        float far = silenceDistance;
        if (sound != null)
        {
            if (sound.FullVolumeDistanceOverride >= 0f) near = sound.FullVolumeDistanceOverride;
            if (sound.SilenceDistanceOverride >= 0f) far = sound.SilenceDistanceOverride;
        }

        source.minDistance = near;
        // Guarded: min >= max makes Unity clamp silently and the sound simply stops fading,
        // which looks exactly like the bug this replaced.
        source.maxDistance = Mathf.Max(far, near + 0.1f);
    }

    /// <summary>
    /// Pushes an edited distance onto voices that already exist.
    ///
    /// Without this the fields are only read when a source is created, so dragging them in play
    /// mode does nothing to the pool and nothing to a loop that is already running - and a cook
    /// loop is the one sound you would most want to tune while listening to it.
    /// </summary>
    void OnValidate()
    {
        // Pool voices are configured per play, so this only matters for whatever is sounding
        // right now; they pick the new defaults up on their own from the next play onward.
        if (_voices != null)
            foreach (var v in _voices)
                if (v != null) ApplyRolloff(v, null);

        // Loops know their own sound, so a default change lands correctly even on a loop that
        // overrides one of the two radii.
        foreach (var entry in _loops.Values)
            if (entry.Source != null) ApplyRolloff(entry.Source, entry.Sound);
    }

    /// <summary>
    /// Creates the fixed pool of AudioSources every sound plays through, configured
    /// for the farm-sized linear rolloff described on the fields above.
    /// </summary>
    void BuildVoices()
    {
        // A domain reload wipes the array but leaves the voice objects parented here, so
        // clear them out rather than ending up with two pools.
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

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
            ApplyRolloff(source, null);

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
        _ambienceSource.Play();
        ApplyAmbienceVolume();
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

        // Per play, not once at build: voices are pooled, so this one is still carrying the
        // distances of whatever sound used it last.
        ApplyRolloff(voice, sound);

        float authored = sound.SpatialBlendOverride;
        voice.spatialBlend = !positional ? 0f
                           : authored >= 0f ? authored
                           : worldSpatialBlend;

        // PlayDelayed rather than a coroutine: the voice is claimed NOW, so the rate limit and
        // the voice-stealing order both see it as busy for the whole wait. A coroutine would
        // leave the voice free in between and let a later sound take it out from underneath.
        float wait = sound.StartDelay;
        if (wait > 0f) voice.PlayDelayed(wait);
        else voice.Play();

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
