using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ชุดควบคุมของด่าน (คันโยกส่งยา + ปุ่ม ▶ + ปุ่มรีเซ็ต) รวมเป็น prefab เดียว
    /// ลาก prefab ลงด่านแล้วย้ายที่รากอันเดียว ไม่ต้องจัดวางแยกทีละปุ่ม
    /// ปุ่ม UI อยู่ใน Canvas แบบ World Space ของ prefab เอง จึงไม่ผูกกับ Canvas ของแมพหรือกล้อง
    /// ตัวปุ่มหา PotionStagingManager / ResetStageController เองในซีน
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class ControlPanel : MonoBehaviour
    {
        private void Awake()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
                canvas.worldCamera = Camera.main;
        }
    }
}
