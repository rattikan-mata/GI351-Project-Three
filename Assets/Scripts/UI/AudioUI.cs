using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioUI : MonoBehaviour
{
    // Singleton เพื่อให้เรียกใช้งานได้จากทุกที่และทำงานข้าม Scene ได้
    public static AudioUI Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource uiSource;

    [Header("Volume")]
    [Tooltip("ความดังรวมของเสียง UI ทั้งหมด (คูณกับ Master x SFX ของ AudioManager และเงียบตาม Mute)")]
    [Range(0f, 1f)] [SerializeField] private float uiVolume = 0.6f;

    [Header("--- Buttons SFX ---")]
    [Tooltip("เสียงกดปุ่ม Play")]
    public AudioClip sfxPlay;

    [Tooltip("เสียงกดปุ่ม Exit ออกเกม")]
    public AudioClip sfxExit;

    [Tooltip("เสียงกดปุ่มแท็บ 3 ขีด (Info/Pause)")]
    public AudioClip sfxTab;

    [Tooltip("เสียงกดปุ่ม Resume เล่นต่อ")]
    public AudioClip sfxResume;

    [Tooltip("เสียงกดปุ่ม Quit กลับ MainMenu")]
    public AudioClip sfxQuit;

    [Tooltip("เสียงกดปุ่ม Reset เริ่มใหม่")]
    public AudioClip sfxReset;

    [Tooltip("เสียงกดปุ่ม MainMenu ในหน้า EndGame")]
    public AudioClip sfxMainMenu;

    [Tooltip("เสียงกดปุ่ม Restart ในหน้า EndGame")]
    public AudioClip sfxRestart;

    [Tooltip("เสียงกดปุ่ม Next ด่านถัดไป")]
    public AudioClip sfxNext;

    [Header("--- Transition & Star SFX ---")]
    [Tooltip("เสียงม่านเลื่อนลงมาปิด")]
    public AudioClip sfxCurtain;

    [Tooltip("เสียงหักดาวเมื่อกด Reset")]
    public AudioClip sfxStarDeduct;

    [Header("--- Panel Endgame SFX ---")]
    [Tooltip("เสียงตอนเปิดหน้าต่างชนะ")]
    public AudioClip sfxWin;

    [Tooltip("เสียงตอนเปิดหน้าต่างแพ้")]
    public AudioClip sfxLose;

    [Tooltip("เสียงตอนดาวเด้งขึ้นมาทีละดวง")]
    public AudioClip sfxStarCount;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (uiSource == null)
        {
            uiSource = GetComponent<AudioSource>();
        }

        uiSource.playOnAwake = false;
        uiSource.loop = false;
        uiSource.spatialBlend = 0f;
    }

    public void PlaySFX(AudioClip clip, float pitch = 1f)
    {
        if (clip != null && uiSource != null)
        {
            float volume = CurrentVolume();
            if (volume <= 0f) return;

            uiSource.pitch = pitch;
            uiSource.PlayOneShot(clip, volume);
        }
    }

    // ความดังจริง = UI Volume x Master x SFX ของเกม (ปิดเสียงเกมแล้ว UI เงียบด้วย)
    private float CurrentVolume()
    {
        var audio = AudioManager.Instance;
        if (audio == null) return uiVolume;
        if (audio.IsMuted) return 0f;
        return uiVolume * audio.MasterVolume * audio.SfxVolume;
    }

    public void PlayBtnPlay() => PlaySFX(sfxPlay);
    public void PlayBtnExit() => PlaySFX(sfxExit);
    public void PlayBtnTab() => PlaySFX(sfxTab);
    public void PlayBtnResume() => PlaySFX(sfxResume);
    public void PlayBtnQuit() => PlaySFX(sfxQuit);
    public void PlayBtnReset() => PlaySFX(sfxReset);
    public void PlayBtnMainMenu() => PlaySFX(sfxMainMenu);
    public void PlayBtnRestart() => PlaySFX(sfxRestart);
    public void PlayBtnNext() => PlaySFX(sfxNext);
}