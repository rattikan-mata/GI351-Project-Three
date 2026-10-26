using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ถังขยะ (Drop Zone): ลบยาออกจาก Result Slot ถาวร
    /// - ทุกขวด (รวมยาที่ใช้ไม่ได้) ถูกทิ้งทันที ไม่คืนวัตถุดิบ ไม่คืนถาด
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class TrashBin2D : MonoBehaviour
    {
        /// <returns>true ถ้าลบยาออกจาก ResultSlot สำเร็จ</returns>
        public bool Discard(ResultSlot slot, int index, PotionInstance potion)
        {
            if (slot == null || potion == null) return false;

            // ตรวจว่ายาอยู่ช่องนั้นจริง (กัน index เพี้ยน) ถ้าไม่ใช่ให้หาช่องที่ยาขวดนี้อยู่
            if (index < 0 || index >= ResultSlot.Capacity || slot.Potions[index] != potion)
            {
                index = -1;
                for (int i = 0; i < ResultSlot.Capacity; i++)
                {
                    if (slot.Potions[i] == potion) { index = i; break; }
                }
                if (index < 0) return false;
            }

            if (!slot.TryTake(index, out _)) return false;

            Debug.Log($"[Trash] ทิ้งยา {potion.DisplayName}");
            CraftingSfx.Play(CraftingSfx.PotionTrash);
            return true;
        }
    }
}
