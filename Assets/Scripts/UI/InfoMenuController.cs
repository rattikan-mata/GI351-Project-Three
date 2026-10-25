using UnityEngine;
using UnityEngine.SceneManagement;

public class InfoMenuController : MonoBehaviour
{
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Awake()
    {
        if (infoPanel == null) infoPanel = gameObject;
        CloseInfo();
    }

    // Button tab
    public void OpenInfo()
    {
        infoPanel.SetActive(true);
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        Time.timeScale = 0f;
    }

    // Resume
    public void CloseInfo()
    {
        Time.timeScale = 1f;
        infoPanel.SetActive(false);
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
    }

    // Quit
    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}