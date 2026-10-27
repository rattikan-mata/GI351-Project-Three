using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบบจัดการเสียงส่วนกลางของเกม (AudioManager):
/// - Singleton เข้าถึงได้จากทุกที่ผ่าน AudioManager.Instance
/// - ไม่ถูกทำลายข้ามฉาก (DontDestroyOnLoad)
/// - มีช่องควบคุมระดับเสียงแยก Master / BGM / SFX พร้อมบันทึกค่าลง PlayerPrefs อัตโนมัติ
/// - เล่น BGM พร้อมระบบ Fade in / Fade out / Crossfade
/// - เล่น SFX ทั้งแบบ 2D และแบบ 3D ณ ตำแหน่งที่ระบุ
/// - รองรับการใส่คลังเสียง (Sound Library) เพื่อให้ฝ่ายอื่นสามารถเรียกเล่นเสียงผ่านชื่อได้ เช่น AudioManager.Instance.PlaySFX("Click")
/// </summary>
public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;
    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<AudioManager>();
                if (instance == null)
                {
#if UNITY_EDITOR
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("Audio Manager t:Prefab");
                    if (guids != null && guids.Length > 0)
                    {
                        foreach (var g in guids)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                            if (path.EndsWith("Audio Manager.prefab", System.StringComparison.OrdinalIgnoreCase))
                            {
                                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                                if (prefab != null)
                                {
                                    GameObject go = Instantiate(prefab);
                                    go.name = "Audio Manager";
                                    instance = go.GetComponent<AudioManager>();
                                    break;
                                }
                            }
                        }
                    }
#endif
                }
            }
            return instance;
        }
        private set => instance = value;
    }

    [System.Serializable]
    public class NamedAudioClip
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float defaultVolume = 1f;
        [Tooltip("ระยะเวลาคูลดาวน์ก่อนที่เสียงนี้จะเล่นซ้ำได้อีก (วินาที) เช่น 0.05 ถึง 0.3 ป้องกันเสียงดังซ้อนรัวๆ (0 = ไม่มีคูลดาวน์)")]
        [Min(0f)] public float cooldown = 0.05f;

        public NamedAudioClip()
        {
            defaultVolume = 1f;
            cooldown = 0.05f;
        }

        public NamedAudioClip(string name, AudioClip clip = null, float defaultVolume = 1f, float cooldown = 0.05f)
        {
            this.name = name;
            this.clip = clip;
            this.defaultVolume = defaultVolume;
            this.cooldown = cooldown;
        }
    }

    #region Inspector Settings
    [Header("Volume Settings (0.0 ถึง 1.0)")]
    [Range(0f, 1f)] [SerializeField] private float defaultMasterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float defaultBgmVolume = 0.75f;
    [Range(0f, 1f)] [SerializeField] private float defaultSfxVolume = 1f;

    [Header("SFX Cooldown Settings (ป้องกันเสียงซ้อนกันรัวๆ)")]
    [Tooltip("ระยะคูลดาวน์เริ่มต้นสำหรับเสียงที่ไม่ได้ตั้งค่าคูลดาวน์แยกไว้ (วินาที)")]
    [Min(0f)] [SerializeField] private float defaultSfxCooldown = 0.05f;

    [Header("Default Music (BGM ตอนเริ่มเกม)")]
    [Tooltip("เพลงประกอบพื้นหลังที่จะเล่นทันทีเมื่อเริ่มเกม (ถ้ามี)")]
    [SerializeField] private AudioClip defaultBgmClip;
    [SerializeField] private bool autoPlayBgmOnStart = true;

    [Header("Sound Library (คลังเสียงเรียกตามชื่อสำหรับทีมงาน)")]
    [Tooltip("ใส่เสียงที่ต้องการให้เรียกเล่นผ่านชื่อสตริงได้ เช่น 'DoorOpen', 'ButtonPress', 'DrinkPotion'")]
    [SerializeField] private List<NamedAudioClip> sfxLibrary = new List<NamedAudioClip>();

    [ContextMenu("Setup Default Sound Keys (ใส่ชื่อเสียงทั้งหมดลงช่องอัตโนมัติ)")]
    public void SetupDefaultSoundKeys()
    {
        string[] defaultKeys = new string[]
        {
            "BoxBreak",
            "BoxPush",
            "SwitchPress",
            "DoorOpen",
            "DoorClose",
            "DoorBreak",
            "BeltRunning",
            "DropperShoot",
            "TrampolineBounce",
            "FlameLoop",
            "AnimalDrinkPotion",
            "AnimalDrink",
            "AnimalCry",
            "GoatPush",
            "GoatSmash",
            "GoatLaser",
            "GoatSkywardLaser",
            "GoatHavoc",
            "GoatGravity",
            "CowFly",
            "CowBounce",
            "CowRam",
            "CowTeleport",
            "CatVomit",
            "CatBubble",
            "CatBubblePop",
            "CatSkyward"
        };

        var existing = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var item in sfxLibrary)
        {
            if (item != null && !string.IsNullOrEmpty(item.name))
            {
                existing.Add(item.name);
                if (item.defaultVolume <= 0.001f) item.defaultVolume = 1f;
                if (item.cooldown < 0f) item.cooldown = 0.05f;
            }
        }

        foreach (var key in defaultKeys)
        {
            if (!existing.Contains(key))
            {
                float cd = (key == "BoxPush" || key == "SwitchPress") ? 0.15f : (key == "CatVomit" ? 0.2f : (key == "CowTeleport" ? 0.1f : 0.05f));
                sfxLibrary.Add(new NamedAudioClip(key, null, 1f, cd));
                existing.Add(key);
            }
        }
        BuildSfxDictionary();
    }
    #endregion

    #region Audio Sources
    private AudioSource bgmSourceA;
    private AudioSource bgmSourceB;
    private bool isSourceAActive = true;
    private Coroutine bgmFadeCoroutine;

    private readonly List<AudioSource> sfxSourcePool = new List<AudioSource>();
    private const int SFX_POOL_SIZE = 12;
    private Dictionary<string, NamedAudioClip> sfxDict;
    private Dictionary<AudioClip, float> clipVolumeDict;
    private Dictionary<AudioClip, float> clipCooldownDict;
    private readonly Dictionary<AudioClip, float> lastPlayTimeByClip = new Dictionary<AudioClip, float>();
    private readonly Dictionary<string, float> lastPlayTimeByName = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
    #endregion

    #region State & Properties
    public float MasterVolume { get; private set; } = 1f;
    public float BgmVolume { get; private set; } = 0.75f;
    public float SfxVolume { get; private set; } = 1f;
    public bool IsMuted { get; private set; } = false;

    private const string PREF_MASTER = "Audio_MasterVolume";
    private const string PREF_BGM = "Audio_BgmVolume";
    private const string PREF_SFX = "Audio_SfxVolume";
    private const string PREF_MUTE = "Audio_IsMuted";
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        LoadPreferences();
        InitializeAudioSources();
        BuildSfxDictionary();
    }

    private void Start()
    {
        if (autoPlayBgmOnStart && defaultBgmClip != null && (bgmSourceA != null && !bgmSourceA.isPlaying))
        {
            PlayBGM(defaultBgmClip);
        }
    }

    private void OnValidate()
    {
        BuildSfxDictionary();
        UpdateAllVolumes();
    }
    #endregion

    #region Initialization
    private void InitializeAudioSources()
    {
        // 1. แหล่งกำเนิด BGM 2 ตัวสำหรับทำ Crossfade เพลง
        bgmSourceA = gameObject.AddComponent<AudioSource>();
        bgmSourceA.loop = true;
        bgmSourceA.playOnAwake = false;

        bgmSourceB = gameObject.AddComponent<AudioSource>();
        bgmSourceB.loop = true;
        bgmSourceB.playOnAwake = false;

        // 2. แหล่งกำเนิด SFX Pool สำหรับเล่นเสียงเอฟเฟกต์ไม่ให้สะดุด
        for (int i = 0; i < SFX_POOL_SIZE; i++)
        {
            AudioSource sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.loop = false;
            sfxSourcePool.Add(sfx);
        }

        UpdateAllVolumes();
    }

    private void BuildSfxDictionary()
    {
        sfxDict = new Dictionary<string, NamedAudioClip>(System.StringComparer.OrdinalIgnoreCase);
        clipVolumeDict = new Dictionary<AudioClip, float>();
        clipCooldownDict = new Dictionary<AudioClip, float>();

        foreach (var item in sfxLibrary)
        {
            if (item != null)
            {
                if (!string.IsNullOrEmpty(item.name))
                {
                    sfxDict[item.name] = item;
                }

                if (item.clip != null)
                {
                    clipVolumeDict[item.clip] = item.defaultVolume;
                    clipCooldownDict[item.clip] = item.cooldown;
                }
            }
        }
    }

    /// <summary>ดึงระดับความดังพื้นฐานของคลิปเสียงที่ตั้งไว้ใน Sound Library</summary>
    public float GetClipBaseVolume(AudioClip clip)
    {
        if (clip == null) return 1f;
        if (clipVolumeDict == null) BuildSfxDictionary();
        return (clipVolumeDict != null && clipVolumeDict.TryGetValue(clip, out float v)) ? v : 1f;
    }

    /// <summary>ดึงระดับความดังพื้นฐานของเสียงผ่านชื่อที่ตั้งไว้ใน Sound Library</summary>
    public float GetSoundBaseVolume(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return 1f;
        if (sfxDict == null) BuildSfxDictionary();
        return (sfxDict != null && sfxDict.TryGetValue(soundName, out NamedAudioClip item)) ? item.defaultVolume : 1f;
    }

    /// <summary>ดึงระยะคูลดาวน์ของคลิปเสียง (วินาที)</summary>
    public float GetClipCooldown(AudioClip clip)
    {
        if (clip == null) return defaultSfxCooldown;
        if (clipCooldownDict == null) BuildSfxDictionary();
        return (clipCooldownDict != null && clipCooldownDict.TryGetValue(clip, out float cd)) ? cd : defaultSfxCooldown;
    }

    /// <summary>ดึงระยะคูลดาวน์ของเสียงผ่านชื่อ (วินาที)</summary>
    public float GetSoundCooldown(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return defaultSfxCooldown;
        if (sfxDict == null) BuildSfxDictionary();
        return (sfxDict != null && sfxDict.TryGetValue(soundName, out NamedAudioClip item)) ? item.cooldown : defaultSfxCooldown;
    }

    /// <summary>ตรวจว่าคลิปเสียงนี้พ้นระยะคูลดาวน์แล้วหรือยัง (true = พร้อมเล่น)</summary>
    public bool CanPlaySFX(AudioClip clip)
    {
        if (clip == null) return false;
        float cd = GetClipCooldown(clip);
        if (cd <= 0.0001f) return true;

        if (lastPlayTimeByClip.TryGetValue(clip, out float lastTime))
        {
            if (Time.unscaledTime - lastTime < cd) return false;
        }
        return true;
    }

    /// <summary>ตรวจว่าเสียงตามชื่อนี้พ้นระยะคูลดาวน์แล้วหรือยัง (true = พร้อมเล่น)</summary>
    public bool CanPlaySFX(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return false;
        float cd = GetSoundCooldown(soundName);
        if (cd <= 0.0001f) return true;

        if (lastPlayTimeByName.TryGetValue(soundName, out float lastTime))
        {
            if (Time.unscaledTime - lastTime < cd) return false;
        }
        return true;
    }

    private void MarkSoundPlayed(AudioClip clip, string soundName = null)
    {
        float now = Time.unscaledTime;
        if (clip != null) lastPlayTimeByClip[clip] = now;
        if (!string.IsNullOrEmpty(soundName)) lastPlayTimeByName[soundName] = now;
    }

    /// <summary>รีเซ็ตคูลดาวน์ของเสียงทั้งหมด</summary>
    public void ResetAllCooldowns()
    {
        lastPlayTimeByClip.Clear();
        lastPlayTimeByName.Clear();
    }

    private void LoadPreferences()
    {
        MasterVolume = PlayerPrefs.GetFloat(PREF_MASTER, defaultMasterVolume);
        BgmVolume = PlayerPrefs.GetFloat(PREF_BGM, defaultBgmVolume);
        SfxVolume = PlayerPrefs.GetFloat(PREF_SFX, defaultSfxVolume);
        IsMuted = PlayerPrefs.GetInt(PREF_MUTE, 0) == 1;
    }
    #endregion

    #region Volume & Mute Controls
    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREF_MASTER, MasterVolume);
        UpdateAllVolumes();
    }

    public void SetBGMVolume(float volume)
    {
        BgmVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREF_BGM, BgmVolume);
        UpdateAllVolumes();
    }

    public void SetSFXVolume(float volume)
    {
        SfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREF_SFX, SfxVolume);
        UpdateAllVolumes();
    }

    public void SetMute(bool muted)
    {
        IsMuted = muted;
        PlayerPrefs.SetInt(PREF_MUTE, IsMuted ? 1 : 0);
        UpdateAllVolumes();
    }

    public void ToggleMute()
    {
        SetMute(!IsMuted);
    }

    private void UpdateAllVolumes()
    {
        float effectiveBgm = IsMuted ? 0f : (MasterVolume * BgmVolume);
        if (bgmSourceA != null) bgmSourceA.volume = effectiveBgm;
        if (bgmSourceB != null) bgmSourceB.volume = effectiveBgm;
    }
    #endregion

    #region BGM Playback
    /// <summary>เล่นเพลง BGM (พร้อมระบบ Smooth Fade/Crossfade)</summary>
    public void PlayBGM(AudioClip clip, bool loop = true, float fadeDuration = 0.5f)
    {
        if (clip == null) return;

        AudioSource current = isSourceAActive ? bgmSourceA : bgmSourceB;
        AudioSource next = isSourceAActive ? bgmSourceB : bgmSourceA;

        // ถ้าเล่นเพลงเดิมอยู่แล้ว ไม่ต้องเริ่มใหม่
        if (current.isPlaying && current.clip == clip) return;

        if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
        bgmFadeCoroutine = StartCoroutine(CrossfadeRoutine(current, next, clip, loop, fadeDuration));
        isSourceAActive = !isSourceAActive;
    }

    /// <summary>หยุดเล่น BGM</summary>
    public void StopBGM(float fadeDuration = 0.5f)
    {
        AudioSource current = isSourceAActive ? bgmSourceA : bgmSourceB;
        if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
        bgmFadeCoroutine = StartCoroutine(FadeOutRoutine(current, fadeDuration));
    }

    private IEnumerator CrossfadeRoutine(AudioSource fromSource, AudioSource toSource, AudioClip nextClip, bool loop, float duration)
    {
        toSource.clip = nextClip;
        toSource.loop = loop;
        toSource.volume = 0f;
        toSource.Play();

        float targetVolume = IsMuted ? 0f : (MasterVolume * BgmVolume);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (fromSource != null && fromSource.isPlaying)
            {
                fromSource.volume = Mathf.Lerp(targetVolume, 0f, t);
            }
            if (toSource != null)
            {
                toSource.volume = Mathf.Lerp(0f, targetVolume, t);
            }
            yield return null;
        }

        if (fromSource != null)
        {
            fromSource.Stop();
            fromSource.volume = 0f;
        }
        if (toSource != null)
        {
            toSource.volume = targetVolume;
        }
        bgmFadeCoroutine = null;
    }

    private IEnumerator FadeOutRoutine(AudioSource source, float duration)
    {
        if (source == null || !source.isPlaying) yield break;

        float startVol = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
            yield return null;
        }

        source.Stop();
        source.volume = 0f;
        bgmFadeCoroutine = null;
    }
    #endregion

    #region SFX Playback
    /// <summary>เล่นเสียง SFX 2D ผ่านคลิปเสียง</summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f, float pitchVariance = 0f, bool ignoreCooldown = false)
    {
        if (clip == null || IsMuted) return;
        if (!ignoreCooldown && !CanPlaySFX(clip)) return;

        MarkSoundPlayed(clip);

        AudioSource source = GetAvailableSfxSource();
        if (source == null) return;

        source.pitch = (pitchVariance > 0f) ? Random.Range(1f - pitchVariance, 1f + pitchVariance) : 1f;
        float clipBaseVol = GetClipBaseVolume(clip);
        float finalVolume = MasterVolume * SfxVolume * clipBaseVol * Mathf.Clamp01(volumeScale);
        source.PlayOneShot(clip, finalVolume);
    }

    /// <summary>แปลงชื่อเรียกเสียงแบบย่อ/ทางเลือก ให้ตรงกับคีย์มาตรฐานในคลังเสียง</summary>
    public static string ResolveSoundAlias(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return null;
        switch (soundName.Trim().ToLowerInvariant())
        {
            case "animaldrink":
            case "animalinject":
            case "drinkpotion":
            case "potiondrink":
            case "animaldrinkpotion":
                return "AnimalDrinkPotion";

            case "animalcry":
            case "cry":
                return "AnimalCry";

            case "belt":
            case "beltloop":
            case "beltrunning":
                return "BeltRunning";

            case "flame":
            case "flameloop":
            case "flamethrower":
            case "fire":
                return "FlameLoop";

            case "dropper":
            case "droppershoot":
            case "dartgun":
            case "dartshoot":
                return "DropperShoot";

            case "trampoline":
            case "tempoline":
            case "trampolinebounce":
            case "bounce":
                return "TrampolineBounce";

            case "boxbreak":
            case "woodboxdestory":
            case "boxdestroy":
                return "BoxBreak";

            case "boxpush":
            case "woodslide":
            case "boxslide":
                return "BoxPush";

            case "switch":
            case "switchpress":
            case "button":
            case "buttonpress":
                return "SwitchPress";

            case "dooropen":
                return "DoorOpen";

            case "doorclose":
                return "DoorClose";

            case "doorbreak":
                return "DoorBreak";

            case "goatpush":
            case "goat":
            case "pusheranimalpush":
            case "goatorange":
                return "GoatPush";

            case "goatsmash":
                return "GoatSmash";

            case "goatlaser":
                return "GoatLaser";

            case "goatskywardlaser":
                return "GoatSkywardLaser";

            case "goathavoc":
            case "havoc":
            case "pusheranimalhavoc":
                return "GoatHavoc";

            case "goatgravity":
            case "goatsurge":
            case "goatceiling":
            case "pusheranimalsurge":
            case "pusheranimalgravity":
            case "gravity":
                return "GoatGravity";

            case "catbubble":
            case "bubble":
                return "CatBubble";

            case "catbubblepop":
            case "bubblepop":
                return "CatBubblePop";

            case "catskyward":
            case "catskywardbubble":
            case "bubbleskyward":
            case "catskywardsound":
                return "CatSkyward";

            case "cowbounce":
            case "cowskyward":
            case "cowskywardbounce":
            case "skywardbounce":
                return "CowBounce";

            default:
                return null;
        }
    }

    /// <summary>เล่นเสียง SFX ผ่านชื่อที่ลงทะเบียนไว้ใน Inspector</summary>
    public void PlaySFX(string soundName, float volumeScale = 1f, float pitchVariance = 0f, bool ignoreCooldown = false)
    {
        if (string.IsNullOrEmpty(soundName) || IsMuted) return;
        if (!ignoreCooldown && !CanPlaySFX(soundName)) return;

        if (sfxDict == null) BuildSfxDictionary();

        NamedAudioClip namedClip = null;
        if (sfxDict != null)
        {
            if (!sfxDict.TryGetValue(soundName, out namedClip) || namedClip.clip == null)
            {
                string alias = ResolveSoundAlias(soundName);
                if (!string.IsNullOrEmpty(alias)) sfxDict.TryGetValue(alias, out namedClip);
            }
        }

        if (namedClip != null && namedClip.clip != null)
        {
            MarkSoundPlayed(namedClip.clip, soundName);
            PlaySFX(namedClip.clip, volumeScale, pitchVariance, ignoreCooldown: true);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] ไม่พบเสียงชื่อ '{soundName}' ในคลังเสียง (Sound Library)");
        }
    }

    /// <summary>เล่นเสียง SFX ณ ตำแหน่งพิกัดในโลกเกม 3D/2D</summary>
    public void PlaySFXAt(AudioClip clip, Vector3 position, float volumeScale = 1f, float pitchVariance = 0f, bool ignoreCooldown = false)
    {
        if (clip == null || IsMuted) return;
        if (!ignoreCooldown && !CanPlaySFX(clip)) return;

        MarkSoundPlayed(clip);

        float clipBaseVol = GetClipBaseVolume(clip);
        float finalVolume = MasterVolume * SfxVolume * clipBaseVol * Mathf.Clamp01(volumeScale);
        float pitch = (pitchVariance > 0f) ? Random.Range(1f - pitchVariance, 1f + pitchVariance) : 1f;

        // สร้าง GameObject เล่นเสียงชั่วคราว ณ ตำแหน่งนั้น
        GameObject soundObj = new GameObject("OneShotSFX_" + clip.name);
        soundObj.transform.position = position;

        AudioSource source = soundObj.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = finalVolume;
        source.pitch = pitch;
        source.spatialBlend = 0f; // 2D flat volume สำหรับเกมแนวนี้
        source.Play();

        Destroy(soundObj, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }

    /// <summary>เล่นเสียง SFX ผ่านชื่อ ณ ตำแหน่งพิกัด</summary>
    public void PlaySFXAt(string soundName, Vector3 position, float volumeScale = 1f, float pitchVariance = 0f, bool ignoreCooldown = false)
    {
        if (string.IsNullOrEmpty(soundName) || IsMuted) return;
        if (!ignoreCooldown && !CanPlaySFX(soundName)) return;

        AudioClip clip = GetClip(soundName);
        if (clip != null)
        {
            MarkSoundPlayed(clip, soundName);
            PlaySFXAt(clip, position, volumeScale, pitchVariance, ignoreCooldown: true);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] ไม่พบเสียงชื่อ '{soundName}' ในคลังเสียง (Sound Library)");
        }
    }

    /// <summary>ดึง AudioClip จากคลังเสียง Sound Library ผ่านชื่อ (รองรับชื่อทางเลือก)</summary>
    public AudioClip GetClip(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return null;
        if (sfxDict == null) BuildSfxDictionary();

        if (sfxDict != null && sfxDict.TryGetValue(soundName, out NamedAudioClip item) && item.clip != null)
        {
            return item.clip;
        }

        string alias = ResolveSoundAlias(soundName);
        if (!string.IsNullOrEmpty(alias) && sfxDict != null && sfxDict.TryGetValue(alias, out NamedAudioClip aliasItem) && aliasItem.clip != null)
        {
            return aliasItem.clip;
        }

        return null;
    }

    private AudioSource GetAvailableSfxSource()
    {
        for (int i = 0; i < sfxSourcePool.Count; i++)
        {
            if (!sfxSourcePool[i].isPlaying) return sfxSourcePool[i];
        }

        // ถ้าเต็มหมด ให้สร้างเพิ่มใน pool
        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        newSource.playOnAwake = false;
        newSource.loop = false;
        sfxSourcePool.Add(newSource);
        return newSource;
    }
    #endregion
}
