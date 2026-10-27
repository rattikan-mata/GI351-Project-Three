using UnityEngine;
using UnityEngine.InputSystem;

namespace PotionSystem
{
    /// <summary>
    /// ขวดยาชั่วคราวที่ลอยตามเมาส์ตอนลาก (มาจากชั้นวาง หรือจากช่องเตรียมยิง) ปล่อยเมาส์แล้ว:
    ///   ช่องเตรียมยิง -> วางลงช่องนั้น (ถ้ามียาอยู่แล้ว ยาเดิมสลับกลับไปที่ที่ลากมา)
    ///   ชั้นวาง        -> คืนเข้าชั้น
    ///   ที่อื่น        -> กลับที่เดิม (ช่องเตรียมยิงเดิม หรือชั้นวาง)
    /// </summary>
    public class PotionDragGhost : MonoBehaviour
    {
        private PotionInstance potion;
        private PotionStagingSlot2D origin; // null = ลากมาจากชั้นวาง
        private PotionShelf shelf;
        private bool dropped;

        public static void Spawn(PotionInstance potion, PotionStagingSlot2D origin, PotionShelf shelf, float box, int sortingOrder)
        {
            var go = new GameObject("DraggedPotion");
            go.transform.position = CraftingSprites2D.MouseWorldPosition();

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            CraftingSprites2D.FitPotion(sr, potion.Data, CraftingSprites2D.ColorFor(potion.Data), box);

            var ghost = go.AddComponent<PotionDragGhost>();
            ghost.potion = potion;
            ghost.origin = origin;
            ghost.shelf = shelf != null ? shelf : FindAnyObjectByType<PotionShelf>();
        }

        private void Update()
        {
            if (dropped) return;

            // ไม่ได้กดเมาส์ค้างอยู่แล้ว = ปล่อยแล้ว (โปรเจกต์นี้ใช้ Input System ใหม่ ห้ามใช้ Input.GetMouseButton)
            bool held = Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (!held)
            {
                dropped = true;
                try { Drop(); }
                catch (System.Exception e)
                {
                    // เกิด error ระหว่างวาง -> คืนยากลับที่เดิมเสมอ ไม่ให้ยาหาย
                    Debug.LogException(e);
                    ReturnHome(potion);
                }
                finally
                {
                    Destroy(gameObject);
                }
                return;
            }

            transform.position = CraftingSprites2D.MouseWorldPosition();
        }

        private void Drop()
        {
            Vector2 point = CraftingSprites2D.MouseWorldPosition();
            foreach (var hit in Physics2D.OverlapPointAll(point))
            {
                // 1) ช่องเตรียมยิง (คนละช่องกับที่ลากมา)
                var target = hit.GetComponent<PotionStagingSlot2D>();
                if (target != null && target != origin)
                {
                    PotionInstance displaced = target.StoredInstance;
                    target.Place(potion);
                    if (displaced != null) ReturnHome(displaced);
                    CraftingSfx.Play(CraftingSfx.PotionPlace);
                    return;
                }

                // 2) ชั้นวาง -> คืนเข้าชั้น
                if (hit.GetComponentInParent<PotionShelfSlot2D>() != null || hit.GetComponentInParent<PotionShelf>() != null)
                {
                    if (shelf != null && shelf.Add(potion.Data))
                    {
                        CraftingSfx.Play(CraftingSfx.PotionPlace);
                        return;
                    }
                }
            }

            // 3) ที่อื่น -> กลับที่เดิม
            ReturnHome(potion);
        }

        private void ReturnHome(PotionInstance p)
        {
            if (p == null) return;
            if (origin != null && !origin.IsOccupied) origin.Place(p);
            else if (shelf != null) shelf.Add(p.Data);
        }
    }
}
