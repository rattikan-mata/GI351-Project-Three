using UnityEngine;
using UnityEngine.SceneManagement;

public class ResetStageController : MonoBehaviour
{
    [SerializeField] private CurtainEffect curtain;
    [SerializeField] private HUDStarSystem starSystem;

    public void OnClickResetStage()
    {
        if (starSystem != null)
        {
            starSystem.DeductOneStar();
        }

        if (AudioUI.Instance != null)
        {
            AudioUI.Instance.PlaySFX(AudioUI.Instance.sfxStarDeduct);
        }

        if (HUDStarSystem.CurrentStars <= 0)
        {
            return;
        }

        if (curtain != null)
        {
            curtain.PlayCurtainTransition(() =>
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            });
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}