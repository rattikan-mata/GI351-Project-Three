using UnityEngine;
using UnityEngine.SceneManagement;

public class ResetStageController : MonoBehaviour
{
    [SerializeField] private CurtainEffect curtain;
    [SerializeField] private HUDStarSystem starSystem;

    public void OnClickReset()
    {
        if (starSystem != null)
        {
            starSystem.DeductOneStar();
        }

        if (HUDStarSystem.CurrentStars <= 0)
        {
            return;
        }

        if (curtain != null)
        {
            curtain.PlayFullResetSequence(() =>
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