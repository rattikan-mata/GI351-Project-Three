using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ตัวช่วยเทส: ใส่ยาสำเร็จรูปลงช่องล่าง (ResultSlot) ตั้งแต่เริ่มเกม ไม่ต้องผสมเอง
    /// วางไว้ใน Scene ตัวเดียว (ใช้ Prefab "Test Potions") แล้วเพิ่ม/ลบยาในลิสต์ได้
    /// ปิดใช้งานด้วยการไม่ติ๊ก Spawn On Start หรือปิด GameObject นี้ทิ้งตอนเล่นจริง
    /// </summary>
    public class TestPotionSpawner : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("ช่องล่างที่จะใส่ยาลงไป ว่างไว้ = หา ResultSlot ใน Scene ให้เอง")]
        [SerializeField] private ResultSlot resultSlot;

        [Header("Potions (ใส่ได้สูงสุด 5 ขวด)")]
        [Tooltip("ยาที่จะใส่ตอนเริ่มเกม เรียงตามช่อง 1-5 ซ้ำชนิดเดิมได้")]
        [SerializeField] private List<PotionData> potions = new List<PotionData>();

        [Header("Settings")]
        [Tooltip("ติ๊กไว้ = ใส่ยาให้ตอนเริ่มเกม")]
        [SerializeField] private bool spawnOnStart = true;

        private void Start()
        {
            if (spawnOnStart) Spawn();
        }

        /// <summary>ใส่ยาในลิสต์ลงช่องล่าง (เรียกจากที่อื่นหรือจากเมนู Context ก็ได้)</summary>
        [ContextMenu("Spawn Potions Now")]
        public void Spawn()
        {
            if (resultSlot == null) resultSlot = FindFirstObjectByType<ResultSlot>();
            if (resultSlot == null)
            {
                Debug.LogWarning("[TestPotionSpawner] ไม่เจอ ResultSlot ใน Scene จึงใส่ยาไม่ได้", this);
                return;
            }

            foreach (var potion in potions)
            {
                if (potion == null) continue;

                // วัตถุดิบเป็น null = ทิ้งยาลงถังขยะแล้วไม่คืนวัตถุดิบ (ยาเทสไม่ได้มาจากการผสมจริง)
                if (!resultSlot.TryAdd(new PotionInstance(potion, null)))
                {
                    Debug.LogWarning("[TestPotionSpawner] ช่องล่างเต็มแล้ว ใส่ยาที่เหลือไม่ได้", this);
                    break;
                }
            }
        }
    }
}
