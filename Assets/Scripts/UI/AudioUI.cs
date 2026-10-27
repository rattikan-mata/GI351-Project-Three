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

    [Header("Auto Balance (ปรับความดังเสียง UI ให้ใกล้กัน)")]
    [Tooltip("ติ๊กไว้ = วัดความดังของแต่ละคลิปตอนเล่นครั้งแรก แล้วลดคลิปที่ดังเกินให้เข้าใกล้ระดับเป้าหมาย (คลิปที่เบากว่าเป้าหมายไม่ถูกเร่ง)")]
    [SerializeField] private bool autoBalance = true;

    [Tooltip("ความดังเป้าหมายของคลิป (dB RMS ส่วนที่มีเสียง) ก่อนคูณ UI Volume ค่ายิ่งใกล้ 0 ยิ่งดัง")]
    [SerializeField, Range(-30f, 0f)] private float targetRmsDb = -12f;

    private readonly System.Collections.Generic.Dictionary<AudioClip, float> balanceGain = new System.Collections.Generic.Dictionary<AudioClip, float>();

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
            float volume = CurrentVolume() * BalanceGain(clip);
            if (volume <= 0f) return;

            uiSource.pitch = pitch;
            uiSource.PlayOneShot(clip, volume);
        }
    }

    // ตัวคูณที่ทำให้คลิปดังเท่าระดับเป้าหมาย (ไม่เกิน 1 คือลดอย่างเดียว) วัดครั้งเดียวแล้วจำไว้
    private float BalanceGain(AudioClip clip)
    {
        if (!autoBalance || clip == null) return 1f;
        if (balanceGain.TryGetValue(clip, out float cached)) return cached;

        float gain = 1f;
        var data = new float[clip.samples * clip.channels];
        if (data.Length > 0 && clip.GetData(data, 0))
        {
            double peak = 0;
            for (int i = 0; i < data.Length; i++) peak = System.Math.Max(peak, System.Math.Abs(data[i]));
            double threshold = peak * 0.05, sum = 0;
            long count = 0;
            for (int i = 0; i < data.Length; i++)
            {
                double v = data[i];
                if (System.Math.Abs(v) > threshold) { sum += v * v; count++; }
            }
            if (count > 0)
            {
                double rmsDb = 20.0 * System.Math.Log10(System.Math.Sqrt(sum / count) + 1e-9);
                gain = Mathf.Clamp01(Mathf.Pow(10f, (float)((targetRmsDb - rmsDb) / 20.0)));
            }
        }
        balanceGain[clip] = gain;
        return gain;
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