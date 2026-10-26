using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ขวดยาที่ปรุงเสร็จ (สร้างโดย ResultSlotDisplay2D) ลากได้ด้วยเมาส์
    /// ปล่อยบน Staging Slot -> ย้ายไปช่องพัก
    /// ปล่อยบนช่องล่างอีกช่อง -> ย้าย/สลับที่กับช่องนั้น
    /// ปล่อยบน TrashBin2D -> ทิ้งยา (ลบถาวร ไม่คืนวัตถุดิบ) / ปล่อยที่อื่น -> วาร์ปกลับ Result Slot เดิม
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class DraggablePotion2D : MonoBehaviour
    {
        private ResultSlot resultSlot;
        private PotionInstance potion;
        private int slotIndex;

        private SpriteRenderer sr;
        private Collider2D col;
        private Transform homeParent;
        private Vector3 grabOffset;
        private int homeSortingOrder;
        private bool dragging;

        public void Init(ResultSlot slot, PotionInstance data, int index)
        {
            resultSlot = slot;
            potion = data;
            slotIndex = index;
            sr = GetComponent<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            col.isTrigger = true;
            homeParent = transform.parent;
            homeSortingOrder = sr.sortingOrder;
        }

        private void OnMouseDown()
        {
            if (potion == null) return;
            dragging = true;
            CraftingSfx.Play(CraftingSfx.PotionPick);
            if (col != null) col.enabled = false;
            grabOffset = transform.position - CraftingSprites2D.MouseWorldPosition();
            sr.sortingOrder = homeSortingOrder + 50;
        }

        private void OnMouseDrag()
        {
            if (!dragging) return;
            transform.position = CraftingSprites2D.MouseWorldPosition() + grabOffset;
        }

        private void OnMouseUp()
        {
            if (!dragging) return;
            dragging = false;
            if (col != null) col.enabled = true;

            Vector2 point = CraftingSprites2D.MouseWorldPosition();
            var hits = Physics2D.OverlapPointAll(point);
            foreach (var hit in hits)
            {
                // 1. ปล่อยลงบน Staging Slot
                var stagingSlot = hit.GetComponent<PotionStagingSlot2D>();
                if (stagingSlot != null)
                {
                    if (stagingSlot.TryAssignPotion(resultSlot, slotIndex, potion))
                    {
                        CraftingSfx.Play(CraftingSfx.PotionPlace);
                        return; // วางสำเร็จ ResultSlot จะอัปเดตตัวเอง
                    }
                    CraftingSfx.Play(CraftingSfx.PotionPlaceFail);
                }

                // 2. ปล่อยลงถังขยะ
                var bin = hit.GetComponent<TrashBin2D>();
                if (bin == null) continue;

                if (bin.Discard(resultSlot, slotIndex, potion))
                    return;
                break;
            }

            // 3. ปล่อยลงบนช่องล่างอีกช่อง -> ย้าย / สลับที่
            if (resultSlot != null)
            {
                foreach (var display in FindObjectsByType<ResultSlotDisplay2D>(FindObjectsSortMode.None))
                {
                    if (display.Slot != resultSlot) continue;
                    if (!display.ContainsPoint(point, out int targetIndex) || targetIndex < 0) continue;

                    // ช่องเดิม = ไม่ต้องทำอะไร ปล่อยให้กลับที่เดิมด้านล่าง
                    if (targetIndex != slotIndex && resultSlot.TryMove(slotIndex, targetIndex))
                    {
                        CraftingSfx.Play(CraftingSfx.PotionPlace);
                        return; // ResultSlotDisplay2D จะสร้างภาพใหม่เอง
                    }
                    break;
                }
            }

            // 4. ปล่อยที่อื่น -> กลับตำแหน่งเดิมใน Result Slot
            transform.position = homeParent != null ? homeParent.position : transform.position;
            sr.sortingOrder = homeSortingOrder;
        }
    }
}