using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ปุ่มรีเซ็ต: หักดาว 1 ดวง แล้วปิดบานพับเฉพาะพื้นที่พัซเซิล -> รีเซ็ตเฉพาะพัซเซิล (PuzzleResetManager) ตอนบานพับปิดสนิท -> เปิดบานพับ
/// ของนอกพัซเซิล (ชั้นขวดยา ช่องเตรียมยิง น้องนักปรุงยา) ไม่ถูกรีเซ็ต
/// ถ้าในซีนไม่มี PuzzleResetManager จะโหลดซีนใหม่ทั้งซีนแทน (แบบเดิม)
/// </summary>
public class ResetStageController : MonoBehaviour
{
    [SerializeField] private CurtainEffect curtain;
    [SerializeField] private HUDStarSystem starSystem;
    [Tooltip("ตัวรีเซ็ตพัซเซิล ว่างไว้ = หาในซีนให้เอง")]
    [SerializeField] private PuzzleResetManager puzzleReset;

    public void OnClickResetStage()
    {
        if (curtain != null && curtain.IsPlaying) return; // กำลังรีเซ็ตอยู่ กดซ้ำไม่นับ
        if (puzzleReset != null && puzzleReset.IsResetting) return;

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

        if (puzzleReset == null) puzzleReset = FindAnyObjectByType<PuzzleResetManager>();

        if (puzzleReset != null)
        {
            if (curtain != null)
                curtain.PlayCloseThenOpen(puzzleReset.ResetPuzzle, () => !puzzleReset.IsResetting);
            else
                puzzleReset.ResetPuzzle();
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
