using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class EndGameUI : MonoBehaviour
{
    [Header("UI Panels & Text")]
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TextMeshProUGUI txtResult;

    [Header("Stars Display")]
    [SerializeField] private Image[] endStarImages;
    [SerializeField] private Sprite activeStarSprite;
    [SerializeField] private Sprite inactiveStarSprite;

    [Header("EndGame Buttons")]
    [SerializeField] private GameObject btnNext;
    [SerializeField] private GameObject btnRestart;
    [SerializeField] private GameObject btnExitToMenu;

    [Header("Scene Navigation Config")]
    [SerializeField] private string nextStageName = "Stage2";
    [SerializeField] private string mainMenuName = "MainMenu";

    private void Awake()
    {
        if (endGamePanel == null) endGamePanel = gameObject;
        endGamePanel.SetActive(false);
    }

    public void OpenWinScreen(int attemptsUsed)
    {
        endGamePanel.SetActive(true);
        txtResult.text = "VICTORY";

        btnNext.SetActive(true);
        btnRestart.SetActive(true);
        btnExitToMenu.SetActive(true);

        int stars = 1;
        if (attemptsUsed <= 1) stars = 3;
        else if (attemptsUsed == 2) stars = 2;
        else if (attemptsUsed >= 3) stars = 1;

        SetStarVisuals(stars);
    }

    public void OpenLoseScreen()
    {
        endGamePanel.SetActive(true);
        txtResult.text = "DEFEAT";

        btnNext.SetActive(false);
        btnRestart.SetActive(true);
        btnExitToMenu.SetActive(true);

        SetStarVisuals(0);
    }

    private void SetStarVisuals(int count)
    {
        for (int i = 0; i < endStarImages.Length; i++)
        {
            if (endStarImages[i] == null) continue;

            if (i < count)
            {
                endStarImages[i].sprite = activeStarSprite;
                endStarImages[i].color = Color.white;
            }
            else
            {
                endStarImages[i].sprite = inactiveStarSprite;
                endStarImages[i].color = new Color(1f, 1f, 1f, 0.25f);
            }
        }
    }

    // --- Buttons Action ---
    public void OnClickNext()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextStageName);
    }

    public void OnClickRestart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnClickExitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuName);
    }
}