using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// แสดงยาสำเร็จรูปใน World Space: สร้าง Sprite ตามจุด 1-5 ตามลำดับที่ ResultSlot ได้รับยา
    /// เพิ่ม: ตรวจได้ว่าตำแหน่งหนึ่งอยู่ในพื้นที่ช่องล่างไหม (ใช้ตอนลากยากลับมาจากช่องพัก)
    /// </summary>
    public class ResultSlotDisplay2D : MonoBehaviour
    {
        #region References
        [SerializeField] private ResultSlot resultSlot;
        [SerializeField] private Transform[] slotPoints = new Transform[ResultSlot.Capacity];
        #endregion

        #region Drop Area
        [Header("Drop Area (ลากยากลับมาวางที่ช่องล่าง)")]
        [Tooltip("(ไม่บังคับ) Collider ที่ครอบทั้งแถวช่องล่าง ถ้าใส่จะใช้ตัวนี้ตรวจ / ถ้าไม่ใส่จะตรวจจากตำแหน่งแต่ละช่องด้วยขนาดด้านล่าง")]
        [SerializeField] private Collider2D dropArea;

        [Tooltip("ขนาดพื้นที่รับยาของแต่ละช่อง (หน่วยโลก) ใช้เมื่อไม่ได้ใส่ Drop Area")]
        [SerializeField] private Vector2 slotDropSize = new Vector2(1.2f, 1.2f);
        #endregion

        public ResultSlot Slot => resultSlot;
        public bool IsFull => resultSlot != null && resultSlot.IsFull;

        /// <summary>
        /// จุด world นี้อยู่บนช่องล่างหรือไม่ และตรงกับช่องที่เท่าไร
        /// slotIndex = -1 ถ้าอยู่ใน Drop Area แต่ไม่ตรงช่องไหนเป๊ะๆ
        /// </summary>
        public bool ContainsPoint(Vector2 worldPoint, out int slotIndex)
        {
            slotIndex = -1;
            float best = float.MaxValue;

            for (int i = 0; i < slotPoints.Length; i++)
            {
                var p = slotPoints[i];
                if (p == null) continue;
                Vector2 d = worldPoint - (Vector2)p.position;
                if (Mathf.Abs(d.x) <= slotDropSize.x * 0.5f && Mathf.Abs(d.y) <= slotDropSize.y * 0.5f
                    && d.sqrMagnitude < best)
                {
                    best = d.sqrMagnitude;
                    slotIndex = i;
                }
            }

            if (slotIndex >= 0) return true;
            return dropArea != null && dropArea.OverlapPoint(worldPoint);
        }

        #region Unity Lifecycle
        private void OnEnable()
        {
            if (resultSlot == null) return;
            resultSlot.OnChanged += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            if (resultSlot != null) resultSlot.OnChanged -= Rebuild;
        }
        #endregion

        #region Rebuild
        private void Rebuild()
        {
            for (int i = 0; i < slotPoints.Length; i++)
            {
                var point = slotPoints[i];
                if (point == null) continue;

                for (int c = point.childCount - 1; c >= 0; c--)
                    Destroy(point.GetChild(c).gameObject);

                if (i >= resultSlot.Potions.Count) continue;

                var potion = resultSlot.Potions[i];
                if (potion == null) continue; // ช่องว่าง
                var go = new GameObject("Potion_" + potion.DisplayName);
                var data = potion.Data;
                go.transform.SetParent(point, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 10;
                CraftingSprites2D.Apply(sr, data, CraftingSprites2D.ColorFor(data), 0.8f);

                // เพิ่ม Collider หลังตั้ง Sprite เพื่อให้ขนาดพอดีกับรูป
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                go.AddComponent<DraggablePotion2D>().Init(resultSlot, potion, i);
            }
        }
        #endregion

        #region Debug Gizmos
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            foreach (var p in slotPoints)
            {
                if (p != null) Gizmos.DrawWireCube(p.position, slotDropSize);
            }
        }
        #endregion
    }
}