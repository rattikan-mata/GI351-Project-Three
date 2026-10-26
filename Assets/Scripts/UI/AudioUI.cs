using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioUI : MonoBehaviour
{
    public static AudioUI Instance { get; private set; }

    [SerializeField] private AudioSource uiSource;

    [Header("Buttons")]
    public AudioClip sfxPlay;
    public AudioClip sfxExit;
    public AudioClip sfxTab;
    public AudioClip sfxResume;
    public AudioClip sfxQuit;
    public AudioClip sfxReset;
    public AudioClip sfxMainMenu;
    public AudioClip sfxRestart;
    public AudioClip sfxNext;

    [Header("Star & Curtain")]
    public AudioClip sfxCurtain;
    public AudioClip sfxStarDeduct;

    [Header("Endgame")]
    public AudioClip sfxWin;
    public AudioClip sfxLose;
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
    }

    public void PlaySFX(AudioClip clip, float pitch = 1f)
    {
        if (clip != null && uiSource != null)
        {
            uiSource.pitch = pitch;
            uiSource.PlayOneShot(clip);
        }
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