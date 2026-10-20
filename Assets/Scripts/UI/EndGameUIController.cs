using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class EndGameUIController : MonoBehaviour
{
    [Header("Panels & Text")]
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TextMeshProUGUI txtTitle;

    [Header("Stars")]
    [SerializeField] private Image[] starImages;
    [SerializeField] private Sprite activeStarSprite;
    [SerializeField] private Sprite inactiveStarSprite;

    [Header("Buttons")]
    [SerializeField] private GameObject btnNextLevel;
    [SerializeField] private GameObject btnRestart;
    [SerializeField] private GameObject btnMainMenu;

    private void Awake()
    {
        if (endGamePanel != null)
        {
            endGamePanel.SetActive(false);
        }

        // ShowWinScreen(1);
        // ShowWinScreen(2);
        // ShowWinScreen(3);
        // ShowLoseScreen();
    }

    public void ShowWinScreen(int attemptsUsed)
    {
        endGamePanel.SetActive(true);
        txtTitle.text = "WIN";

        btnNextLevel.SetActive(true);
        btnRestart.SetActive(true);
        btnMainMenu.SetActive(true);

        int starsEarned = 0;
        if (attemptsUsed == 1) starsEarned = 3;
        else if (attemptsUsed == 2) starsEarned = 2;
        else if (attemptsUsed == 3) starsEarned = 1;

        UpdateStarDisplay(starsEarned);
    }

    public void ShowLoseScreen()
    {
        endGamePanel.SetActive(true);
        txtTitle.text = "LOSE";

        btnNextLevel.SetActive(false);
        btnRestart.SetActive(true);
        btnMainMenu.SetActive(true);

        UpdateStarDisplay(0);
    }

    private void UpdateStarDisplay(int starsCount)
    {
        for (int i = 0; i < starImages.Length; i++)
        {
            if (i < starsCount)
            {
                starImages[i].sprite = activeStarSprite;
                starImages[i].color = Color.white;
            }
            else
            {
                starImages[i].sprite = inactiveStarSprite;
                starImages[i].color = new Color(1f, 1f, 1f, 0.25f);
            }
        }
    }

    public void OnClickNextLevel(string nextSceneName)
    {
        SceneManager.LoadScene(nextSceneName);
    }

    public void OnClickRestart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnClickMainMenu(string mainMenuSceneName)
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}