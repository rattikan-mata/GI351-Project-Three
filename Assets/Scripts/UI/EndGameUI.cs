using System.Collections;
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

    [Header("Star Animation Settings (Script)")]
    [Tooltip("ระยะเวลารอระหว่างดาวแต่ละดวง (วินาที)")]
    [SerializeField] private float delayBetweenStars = 0.4f;
    [Tooltip("ความเร็วในการเด้งขยายของดาวแต่ละดวง (วินาที)")]
    [SerializeField] private float starPopDuration = 0.25f;

    [Header("EndGame Buttons")]
    [SerializeField] private GameObject btnNext;
    [SerializeField] private GameObject btnRestart;
    [SerializeField] private GameObject btnExitToMenu;

    [Header("Scene Navigation Config")]
    [SerializeField] private string nextStageName = "Stage2";
    [SerializeField] private string mainMenuName = "MainMenu";

    private Coroutine starRoutine;

    private void Awake()
    {
        if (endGamePanel == null) endGamePanel = gameObject;
        endGamePanel.SetActive(false);
    }

    public void OpenWinScreen()
    {
        endGamePanel.SetActive(true);
        txtResult.text = "VICTORY";

        btnNext.SetActive(true);
        btnRestart.SetActive(true);
        btnExitToMenu.SetActive(true);

        int starsEarned = HUDStarSystem.CurrentStars;

        if (starRoutine != null) StopCoroutine(starRoutine);
        starRoutine = StartCoroutine(AnimateStarsSequential(starsEarned));
    }

    public void OpenLoseScreen()
    {
        endGamePanel.SetActive(true);
        txtResult.text = "DEFEAT";

        btnNext.SetActive(false);
        btnRestart.SetActive(true);
        btnExitToMenu.SetActive(true);

        if (starRoutine != null) StopCoroutine(starRoutine);
        starRoutine = StartCoroutine(AnimateStarsSequential(0));
    }

    private IEnumerator AnimateStarsSequential(int targetStarCount)
    {
        for (int i = 0; i < endStarImages.Length; i++)
        {
            if (endStarImages[i] != null)
            {
                endStarImages[i].sprite = inactiveStarSprite;
                endStarImages[i].color = new Color(1f, 1f, 1f, 0.25f);
                endStarImages[i].transform.localScale = Vector3.one;
            }
        }

        yield return new WaitForSecondsRealtime(0.25f);

        for (int i = 0; i < targetStarCount; i++)
        {
            if (i < endStarImages.Length && endStarImages[i] != null)
            {
                endStarImages[i].sprite = activeStarSprite;
                endStarImages[i].color = Color.white;

                yield return StartCoroutine(PopStar(endStarImages[i].transform));

                yield return new WaitForSecondsRealtime(delayBetweenStars);
            }
        }
    }

    private IEnumerator PopStar(Transform starTransform)
    {
        float elapsed = 0f;
        Vector3 startScale = Vector3.zero;
        Vector3 peakScale = new Vector3(1.3f, 1.3f, 1f);
        Vector3 endScale = Vector3.one;

        while (elapsed < starPopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / starPopDuration;

            if (t < 0.6f)
            {
                starTransform.localScale = Vector3.Lerp(startScale, peakScale, t / 0.6f);
            }
            else
            {
                starTransform.localScale = Vector3.Lerp(peakScale, endScale, (t - 0.6f) / 0.4f);
            }

            yield return null;
        }

        starTransform.localScale = endScale;
    }

    // --- Buttons Action ---
    public void OnClickNext()
    {
        Time.timeScale = 1f;
        HUDStarSystem.ResetStarsForNewStage();
        SceneManager.LoadScene(nextStageName);
    }

    public void OnClickRestart()
    {
        Time.timeScale = 1f;
        HUDStarSystem.ResetStarsForNewStage();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnClickExitToMenu()
    {
        Time.timeScale = 1f;
        HUDStarSystem.ResetStarsForNewStage();
        SceneManager.LoadScene(mainMenuName);
    }
}