using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ปุ่มรีเซ็ตพัซเซิลที่เป็นวัตถุในฉาก (ไม่ใช่ UI) วางข้างปุ่ม ▶ คลิกแล้วทำเหมือนปุ่ม Reset ของ UI
    /// (หักดาว 1 ดวง ปิดบานพับ รีเซ็ตพัซเซิล) โดยเรียก ResetStageController.OnClickResetStage
    /// ปุ่ม Reset แบบ UI เดิมถูกซ่อนแล้ว (ใน prefab -- UI -- เหลือแต่ตัว ResetStageController ที่ปุ่มนี้เรียกใช้)
    /// ต้องมี Collider2D (ใช้คลิกแบบเดียวกับช่องยาและน้องนักปรุงยา)
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ResetWorldButton : MonoBehaviour
    {
        [Tooltip("ว่างไว้ = หา ResetStageController ในซีนให้เอง")]
        [SerializeField] private ResetStageController controller;
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float pressScale = 0.92f;

        private Vector3 restScale;

        private void Awake()
        {
            restScale = transform.localScale;
        }

        private void OnDisable()
        {
            transform.localScale = restScale;
        }

        private void OnMouseEnter() => transform.localScale = restScale * hoverScale;
        private void OnMouseExit() => transform.localScale = restScale;
        private void OnMouseDown() => transform.localScale = restScale * pressScale;

        private void OnMouseUpAsButton()
        {
            transform.localScale = restScale * hoverScale;
            if (controller == null) controller = FindAnyObjectByType<ResetStageController>(FindObjectsInactive.Include);
            if (controller != null) controller.OnClickResetStage();
        }
    }
}
