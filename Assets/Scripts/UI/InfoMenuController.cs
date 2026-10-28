using UnityEngine;
using UnityEngine.SceneManagement;

public class InfoMenuController : MonoBehaviour
{
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Sound View (หน้าปรับเสียง)")]
    [Tooltip("กลุ่มแถบเลื่อนเสียง BGM/GAME ที่จะโผล่เมื่อกดปุ่ม Sound")]
    [SerializeField] private GameObject soundView;
    [Tooltip("ปุ่มที่ซ่อนตอนอยู่ในหน้าปรับเสียง (Resume, Sound, Quit) ปุ่ม Menu ที่อยู่ใน Sound View จะโผล่แทนที่ปุ่ม Sound ใช้กดย้อนกลับ")]
    [SerializeField] private GameObject[] hideWhileSoundView;
    [Tooltip("ภาพกรอบของหน้าเมนูหลัก (มีคำว่า MENU) จะถูกปิดตอนอยู่หน้าปรับเสียง")]
    [SerializeField] private Behaviour menuPanelGraphic;
    [Tooltip("ภาพกรอบเปล่าของหน้าปรับเสียง จะโผล่เฉพาะตอนอยู่หน้าปรับเสียง")]
    [SerializeField] private GameObject soundPanelArt;

    private void Awake()
    {
        if (infoPanel == null) infoPanel = gameObject;
        CloseInfo();
    }

    /// <summary>ผูกกับปุ่ม Sound: สลับระหว่างหน้าเมนูปกติกับหน้าปรับเสียง</summary>
    public void ToggleSoundView()
    {
        SetSoundView(soundView == null || !soundView.activeSelf);
    }

    private void SetSoundView(bool show)
    {
        if (soundView != null) soundView.SetActive(show);
        if (menuPanelGraphic != null) menuPanelGraphic.enabled = !show;
        if (soundPanelArt != null) soundPanelArt.SetActive(show);
        if (hideWhileSoundView == null) return;
        foreach (var go in hideWhileSoundView)
            if (go != null) go.SetActive(!show);
    }

    public void OpenInfo()
    {
        SetSoundView(false);
        infoPanel.SetActive(true);
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        Time.timeScale = 0f;
    }

    public void CloseInfo()
    {
        Time.timeScale = 1f;
        infoPanel.SetActive(false);
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        HUDStarSystem.ResetStarsForNewStage();
        SceneManager.LoadScene(mainMenuSceneName);
    }
}