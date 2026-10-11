using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ถังขยะ (Drop Zone): รีไซเคิลยา
    /// - ทุกขวด (รวม Death potion): คืนวัตถุดิบที่ใช้ปรุงจริงแบบสุ่ม 1-2 ชิ้น (หรือทั้งหมดถ้าเปิด refundAll) เข้า SourceInventory
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class TrashBin2D : MonoBehaviour
    {
        [SerializeField] private PotionRecipeBook recipeBook;
        [SerializeField] private SourceInventory sourceInventory;
        [Tooltip("เปิด = คืนวัตถุดิบที่ใช้จริงทั้งหมด / ปิด = คืนสุ่ม 1-2 ชิ้นจากที่ใช้จริง")]
        [SerializeField] private bool refundAll = false;

        /// <returns>true ถ้าลบยาออกจาก ResultSlot สำเร็จ</returns>
        public bool Recycle(ResultSlot slot, int index, PotionInstance potion)
        {
            if (slot == null || potion == null) return false;

            // ตรวจว่ายาอยู่ช่องนั้นจริง (กัน index เพี้ยน)
            if (index < 0 || index >= slot.Count || slot.Potions[index] != potion)
            {
                index = -1;
                for (int i = 0; i < slot.Count; i++)
                    if (slot.Potions[i] == potion) { index = i; break; }
                if (index < 0) return false;
            }

            if (!slot.TryTake(index, out _)) return false;

            var pool = new List<RawMaterialData>();
            foreach (var m in potion.UsedMaterials) // วัตถุดิบที่ใช้จริงตอนปรุง
                if (m != null) pool.Add(m);

            int count = refundAll ? pool.Count : Mathf.Min(Random.Range(1, 3), pool.Count); // 1 หรือ 2
            for (int i = 0; i < count; i++)
            {
                int pick = Random.Range(0, pool.Count);
                var m = pool[pick];
                pool.RemoveAt(pick); // ไม่ซ้ำชนิด
                if (sourceInventory != null) sourceInventory.Add(m, 1);
                Debug.Log($"[Trash] คืน {m.DisplayName} x1");
            }
            return true;
        }
    }
}
