using UnityEngine;
using UnityEngine.UI;

public class HUDStarSystem : MonoBehaviour
{
    [Header("HUD Star Images")]
    [SerializeField] private Image[] starImages;
    [SerializeField] private Sprite activeStarSprite;
    [SerializeField] private Sprite inactiveStarSprite;

    public int CurrentStars { get; private set; } = 3;
    public int AttemptCount { get; private set; } = 0;

    private void Awake()
    {
        ResetStars();
    }

    public void ResetStars()
    {
        CurrentStars = 3;
        AttemptCount = 0;
        RefreshVisuals();
    }

    public void DeductOneStar()
    {
        AttemptCount++;
        if (CurrentStars > 0)
        {
            CurrentStars--;
        }
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] == null) continue;

            if (i < CurrentStars)
            {
                starImages[i].sprite = activeStarSprite;
                starImages[i].color = Color.white;
            }
            else
            {
                starImages[i].sprite = inactiveStarSprite;
                starImages[i].color = new Color(1f, 1f, 1f, 0.3f);
            }
        }
    }
}