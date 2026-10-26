using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour
{
    public enum SoundType
    {
        Play,
        Exit,
        Tab,
        Resume,
        Quit,
        Reset,
        MainMenu,
        Restart,
        Next
    }

    [Header("เลือกประเภทเสียงของปุ่มนี้")]
    [SerializeField] private SoundType soundType;

    private void Awake()
    {
        Button btn = GetComponent<Button>();
        btn.onClick.AddListener(PlaySound);
    }

    private void PlaySound()
    {
        if (AudioUI.Instance == null) return;

        switch (soundType)
        {
            case SoundType.Play: AudioUI.Instance.PlayBtnPlay(); break;
            case SoundType.Exit: AudioUI.Instance.PlayBtnExit(); break;
            case SoundType.Tab: AudioUI.Instance.PlayBtnTab(); break;
            case SoundType.Resume: AudioUI.Instance.PlayBtnResume(); break;
            case SoundType.Quit: AudioUI.Instance.PlayBtnQuit(); break;
            case SoundType.Reset: AudioUI.Instance.PlayBtnReset(); break;
            case SoundType.MainMenu: AudioUI.Instance.PlayBtnMainMenu(); break;
            case SoundType.Restart: AudioUI.Instance.PlayBtnRestart(); break;
            case SoundType.Next: AudioUI.Instance.PlayBtnNext(); break;
        }
    }
}