using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ขวดยาที่ปรุงเสร็จ (สร้างโดย ResultSlotDisplay2D) ลากได้ด้วยเมาส์
    /// ปล่อยบน TrashBin2D -> รีไซเคิล / ปล่อยที่อื่น -> วาร์ปกลับ Result Slot เดิม
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class DraggablePotion2D : MonoBehaviour
    {
        private ResultSlot resultSlot;
        private PotionInstance potion;
        private int slotIndex;

        private SpriteRenderer sr;
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
            homeParent = transform.parent;
            homeSortingOrder = sr.sortingOrder;
        }

        private void OnMouseDown()
        {
            if (potion == null) return;
            dragging = true;
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

    var hits = Physics2D.OverlapPointAll(CraftingSprites2D.MouseWorldPosition());
    foreach (var hit in hits)
    {
        // 1. ตรวจสอบว่าปล่อยลงบน Staging Slot หรือไม่
        var stagingSlot = hit.GetComponent<PotionStagingSlot2D>();
        if (stagingSlot != null)
        {
            if (stagingSlot.TryAssignPotion(resultSlot, slotIndex, potion))
                return; // วางสำเร็จ ResultSlot จะอัปเดตตัวเอง
        }

        // 2. ตรวจสอบถังขยะ
        var bin = hit.GetComponent<TrashBin2D>();
        if (bin == null) continue;

        if (bin.Recycle(resultSlot, slotIndex, potion))
            return;
        break;
    }

    // ปล่อยที่อื่น -> กลับตำแหน่งเดิมใน Result Slot
    transform.position = homeParent != null ? homeParent.position : transform.position;
    sr.sortingOrder = homeSortingOrder;
    }
    }
}
