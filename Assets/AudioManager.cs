using UnityEngine;
using System.Collections;

/// <summary>
/// Global SFX manager.
/// Keeps the same public Play... API and supports procedural fallback clips.
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("General")]
    [Tooltip("Source used to play one-shot SFX. Auto-created if empty.")]
    public AudioSource sfxSource;
    [Range(0f, 1f)] public float masterSfxVolume = 0.65f;
    [Range(0f, 0.5f)] public float pitchVariance = 0.05f;
    [Range(0f, 0.5f)] public float volumeVariance = 0.05f;
    [Min(0f)] public float minPlayInterval = 0.03f;

    [Header("Per Event Volume")]
    [Range(0f, 1f)] public float warningVolumeScale = 0.3f;
    [Range(0f, 1f)] public float dashVolumeScale = 0.55f;

    [Header("Pickup Burst")]
    [Min(1)] public int maxPickupBurstCount = 12;
    [Min(0.01f)] public float pickupBurstInterval = 0.045f;

    [Header("Assigned Clips")]
    public AudioClip shootClip;
    public AudioClip hitClip;
    public AudioClip enemyDeadClip;
    public AudioClip playerHurtClip;
    public AudioClip pickupClip;
    public AudioClip dashClip;
    public AudioClip warningClip;
    public AudioClip teleportClip;
    public AudioClip spawnClip;
    public AudioClip uiConfirmClip;

    [Header("Procedural Fallback")]
    [Tooltip("If assigned clip is missing, use generated procedural clip.")]
    public bool useProceduralFallback = true;
    public ProceduralSfxLibrary proceduralLibrary;

    private static AudioManager _instance;
    private float _lastPlayTime = -999f;

    public enum SfxEvent
    {
        Shoot,
        Hit,
        EnemyDead,
        PlayerHurt,
        Pickup,
        Dash,
        Warning,
        Teleport,
        Spawn,
        UIConfirm
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (!sfxSource)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        if (useProceduralFallback)
        {
            EnsureProceduralLibrary();
        }
    }

    public static void PlayShoot() => PlayEvent(SfxEvent.Shoot);
    public static void PlayHit() => PlayEvent(SfxEvent.Hit);
    public static void PlayEnemyDead() => PlayEvent(SfxEvent.EnemyDead);
    public static void PlayPlayerHurt() => PlayEvent(SfxEvent.PlayerHurt);
    public static void PlayPickup() => PlayEvent(SfxEvent.Pickup);
    public static void PlayDash() => PlayEvent(SfxEvent.Dash);
    public static void PlayWarning() => PlayEvent(SfxEvent.Warning);
    public static void PlayTeleport() => PlayEvent(SfxEvent.Teleport);
    public static void PlaySpawn() => PlayEvent(SfxEvent.Spawn);
    public static void PlayUIConfirm() => PlayEvent(SfxEvent.UIConfirm);

    public static void Play(SfxEvent evt)
    {
        PlayEvent(evt);
    }

    public static void PlayPickupBurst(int count)
    {
        if (_instance == null || count <= 0) return;
        _instance.StartCoroutine(_instance.PlayPickupBurstRoutine(count));
    }

    private static void PlayEvent(SfxEvent evt)
    {
        PlayClip(_instance?.ResolveClip(evt), _instance != null ? _instance.GetEventVolumeScale(evt) : 1f);
    }

    private static void PlayClip(AudioClip clip, float eventScale)
    {
        if (_instance == null || clip == null || _instance.sfxSource == null)
            return;

        if (Time.unscaledTime - _instance._lastPlayTime < _instance.minPlayInterval)
            return;

        var src = _instance.sfxSource;
        src.pitch = 1f + Random.Range(-_instance.pitchVariance, _instance.pitchVariance);
        float volume = (1f - Random.Range(0f, _instance.volumeVariance)) * _instance.masterSfxVolume * Mathf.Clamp01(eventScale);
        src.PlayOneShot(clip, volume);
        _instance._lastPlayTime = Time.unscaledTime;
    }

    private float GetEventVolumeScale(SfxEvent evt)
    {
        switch (evt)
        {
            case SfxEvent.Warning:
                return warningVolumeScale; // radar/sonar warning tone
            case SfxEvent.Dash:
                return dashVolumeScale;
            default:
                return 1f;
        }
    }

    private IEnumerator PlayPickupBurstRoutine(int count)
    {
        int times = Mathf.Clamp(count, 1, Mathf.Max(1, maxPickupBurstCount));
        float interval = Mathf.Max(minPlayInterval, pickupBurstInterval);
        for (int i = 0; i < times; i++)
        {
            PlayPickup();
            if (i < times - 1) yield return new WaitForSecondsRealtime(interval);
        }
    }

    private void EnsureProceduralLibrary()
    {
        if (proceduralLibrary != null) return;
        proceduralLibrary = GetComponent<ProceduralSfxLibrary>();
        if (proceduralLibrary == null)
        {
            proceduralLibrary = gameObject.AddComponent<ProceduralSfxLibrary>();
        }
    }

    private AudioClip ResolveClip(SfxEvent evt)
    {
        AudioClip assigned = GetAssignedClip(evt);
        if (assigned != null) return assigned;

        if (!useProceduralFallback) return null;
        if (proceduralLibrary == null) EnsureProceduralLibrary();
        if (proceduralLibrary == null) return null;

        return GetProceduralClip(evt);
    }

    private AudioClip GetAssignedClip(SfxEvent evt)
    {
        switch (evt)
        {
            case SfxEvent.Shoot: return shootClip;
            case SfxEvent.Hit: return hitClip;
            case SfxEvent.EnemyDead: return enemyDeadClip;
            case SfxEvent.PlayerHurt: return playerHurtClip;
            case SfxEvent.Pickup: return pickupClip;
            case SfxEvent.Dash: return dashClip;
            case SfxEvent.Warning: return warningClip;
            case SfxEvent.Teleport: return teleportClip;
            case SfxEvent.Spawn: return spawnClip;
            case SfxEvent.UIConfirm: return uiConfirmClip;
            default: return null;
        }
    }

    private AudioClip GetProceduralClip(SfxEvent evt)
    {
        switch (evt)
        {
            case SfxEvent.Shoot: return proceduralLibrary.shootClip;
            case SfxEvent.Hit: return proceduralLibrary.hitClip;
            case SfxEvent.EnemyDead: return proceduralLibrary.enemyDeadClip;
            case SfxEvent.PlayerHurt: return proceduralLibrary.playerHurtClip;
            case SfxEvent.Pickup: return proceduralLibrary.pickupClip;
            case SfxEvent.Dash: return proceduralLibrary.dashClip;
            case SfxEvent.Warning: return proceduralLibrary.warningClip;
            case SfxEvent.Teleport: return proceduralLibrary.teleportClip;
            case SfxEvent.Spawn: return proceduralLibrary.spawnClip;
            case SfxEvent.UIConfirm: return proceduralLibrary.uiConfirmClip;
            default: return null;
        }
    }
}
