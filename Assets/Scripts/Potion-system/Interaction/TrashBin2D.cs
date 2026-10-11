using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ถังขยะ (Drop Zone): รีไซเคิลยา
    /// - ยาตามสูตร: คืนวัตถุดิบต้นทางแบบสุ่ม 1-2 ชิ้น (จาก 3 ชิ้นของ Blueprint) เข้า SourceInventory
    /// - ยาเสีย (ไม่มี Blueprint ที่ให้ผลลัพธ์นี้): ไม่คืนอะไร
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class TrashBin2D : MonoBehaviour
    {
        [SerializeField] private PotionRecipeBook recipeBook;
        [SerializeField] private SourceInventory sourceInventory;

        /// <returns>true ถ้าลบยาออกจาก ResultSlot สำเร็จ</returns>
        public bool Recycle(ResultSlot slot, int index, PotionData potion)
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

            var blueprint = FindBlueprint(potion);
            if (blueprint == null)
            {
                Debug.Log($"[Trash] ทิ้ง {potion.DisplayName} (ยาเสีย) ไม่ได้อะไรคืน");
                return true;
            }

            var pool = new List<RawMaterialData>();
            foreach (var m in blueprint.UseMaterials)
                if (m != null) pool.Add(m);

            int count = Mathf.Min(Random.Range(1, 3), pool.Count); // 1 หรือ 2
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

        private PotionBlueprint FindBlueprint(PotionData potion)
        {
            if (recipeBook == null) return null;
            foreach (var bp in recipeBook.Blueprints)
                if (bp != null && bp.ResultPotion == potion) return bp;
            return null;
        }
    }
}
