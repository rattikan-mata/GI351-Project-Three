using UnityEngine;
using UnityEngine.InputSystem;

namespace PotionSystem
{
    /// <summary>
    /// ช่องพักยา 1 ใน 3 ช่อง (แถวบน) ก่อนส่งไปยัง Potion Sequence Dropper
    /// - วางยาจากช่องล่าง (ResultSlot) ลงมาได้
    /// - คลิกค้างที่ช่องนี้เพื่อหยิบยาขึ้นมาลาก แล้วปล่อยที่:
    ///     ช่องล่าง (ResultSlot)  -> ยาย้ายกลับไปช่องล่าง
    ///     ช่องพักอีกช่อง         -> สลับ/ย้ายไปช่องนั้น
    ///     ที่อื่น                -> เด้งกลับช่องเดิม (ยาไม่หาย)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class PotionStagingSlot2D : MonoBehaviour
    {
        #region Visual
        [Header("Slot Frame (กรอบ/รูปของช่อง ใช้ SpriteRenderer ของช่องเอง)")]
        [Tooltip("ติ๊กไว้ = ใช้รูปและสีที่ตั้งไว้บน SpriteRenderer ของช่องในฉากเป็นกรอบ ไม่เขียนทับ\n" +
                 "ไม่ติ๊ก = ใช้สี่เหลี่ยมเทาเข้มเป็นกรอบตอนว่าง (และซ่อนกรอบตอนมียา) เหมือนเดิม\n" +
                 "ถ้ากำหนดรูปใน Empty/Filled Slot Sprite ข้างล่าง จะใช้รูปนั้นก่อนเสมอ")]
        [SerializeField] private bool keepSceneFrame = true;

        [Tooltip("รูปกรอบตอนช่องว่าง (ไม่บังคับ)")]
        [SerializeField] private Sprite emptySlotSprite;

        [Tooltip("รูปกรอบตอนช่องมียา (ไม่บังคับ) ว่างไว้ = ใช้รูปเดียวกับตอนว่าง")]
        [SerializeField] private Sprite filledSlotSprite;

        [Tooltip("สีที่ย้อมรูปกรอบ (ใช้เมื่อกำหนด Empty/Filled Slot Sprite เท่านั้น)")]
        [SerializeField] private Color frameColor = Color.white;

        [Header("Potion Icon (รูปยาที่แสดงทับบนกรอบ)")]
        [Tooltip("SpriteRenderer ที่ใช้แสดงรูปยา ว่างไว้ = สร้างลูก (child) ชื่อ PotionIcon ให้เองอัตโนมัติ")]
        [SerializeField] private SpriteRenderer iconRenderer;

        [Tooltip("ลำดับการวาด (Order in Layer) ของรูปยาที่สร้างให้อัตโนมัติ ต้องมากกว่า Sorting Order ของ Canvas ถึงจะอยู่หน้า Canvas\n" +
                 "(Canvas Leyer 1 ใน Stage 1 = 3) ไม่มีผลถ้าใส่ Icon Renderer เอง")]
        [SerializeField] private int iconSortingOrder = 10;

        [Tooltip("ขนาดของขวดยาตอนอยู่ในช่องนี้ (1 = ขนาดปกติ, 0.5 = เล็กลงครึ่งหนึ่ง) ไม่กระทบขนาดตอนลาก และไม่กระทบพื้นที่คลิก")]
        [SerializeField, Range(0.1f, 2f)] private float storedPotionScale = 1f;

        [Tooltip("ขยับรูปยาจากกึ่งกลางช่อง (หน่วย local)")]
        [SerializeField] private Vector2 iconOffset = Vector2.zero;
        #endregion

        #region Drag Settings
        [Header("Drag")]
        [Tooltip("ขนาดของขวดยาตอนลาก")]
        [SerializeField] private float dragScale = 0.8f;
        [SerializeField] private int dragSortingOrder = 100;
        #endregion

        #region State
        private SpriteRenderer frameRenderer;
        private Sprite sceneFrameSprite;
        private Color sceneFrameColor;

        /// <summary>ขวดยาที่เก็บอยู่</summary>
        public PotionInstance StoredInstance { get; private set; }
        public PotionData StoredPotion => StoredInstance != null ? StoredInstance.Data : null;
        public bool IsOccupied => StoredInstance != null;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            frameRenderer = GetComponent<SpriteRenderer>();
            sceneFrameSprite = frameRenderer.sprite;
            sceneFrameColor = frameRenderer.color;

            // ไม่มี Icon Renderer แยก (หรือใส่ตัวเดียวกับกรอบ) = สร้างลูกไว้แสดงรูปยาทับกรอบ
            if (iconRenderer == null || iconRenderer == frameRenderer)
            {
                var iconGo = new GameObject("PotionIcon");
                iconGo.transform.SetParent(transform, false);
                iconRenderer = iconGo.AddComponent<SpriteRenderer>();
                iconRenderer.sortingLayerID = frameRenderer.sortingLayerID;
                iconRenderer.sortingOrder = Mathf.Max(iconSortingOrder, frameRenderer.sortingOrder + 1);
            }

            GetComponent<BoxCollider2D>().isTrigger = true;
            Clear();
        }

        // ปรับค่าในส่วน Visual ใน Inspector ตอนเล่นอยู่ แล้วเห็นผลทันที
        private void OnValidate()
        {
            if (Application.isPlaying && iconRenderer != null && frameRenderer != null) UpdateVisual();
        }

        // คลิกที่ช่องที่มียา = หยิบขึ้นมาลาก (ถ้าปล่อยผิดที่จะกลับมาช่องเดิม)
        private void OnMouseDown()
        {
            if (!IsOccupied) return;
            if (!TryTake(out PotionInstance taken)) return;

            var go = new GameObject("DraggedPotion_FromStaging");
            go.transform.position = CraftingSprites2D.MouseWorldPosition();

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = dragSortingOrder;
            CraftingSprites2D.Apply(sr, taken.Data, CraftingSprites2D.ColorFor(taken.Data), dragScale);

            go.AddComponent<StagingDragGhost>().Init(this, taken);
        }
        #endregion

        #region Public API
        /// <summary>วางยาลงช่องนี้ (มาจาก ResultSlot ช่องล่าง) ถ้าช่องนี้มียาอยู่แล้วจะส่งกลับไปช่องล่าง</summary>
        public bool TryAssignPotion(ResultSlot resultSlot, int resultIndex, PotionInstance potion)
        {
            if (potion == null) return false;

            if (resultSlot != null && !resultSlot.TryTake(resultIndex, out _))
                return false;

            // ช่องนี้มียาอยู่แล้ว -> ส่งยาเก่ากลับไปช่องล่างที่เพิ่งว่าง (สลับที่กัน)
            if (StoredInstance != null && resultSlot != null)
            {
                if (!resultSlot.TryAddAt(resultIndex, StoredInstance))
                    resultSlot.TryAdd(StoredInstance);
            }

            StoredInstance = potion;
            UpdateVisual();
            return true;
        }

        /// <summary>ใส่ยาตรงๆ (ใช้ตอนเด้งกลับ / สลับช่อง)</summary>
        public void Place(PotionInstance potion)
        {
            StoredInstance = potion;
            UpdateVisual();
        }

        public bool TryTake(out PotionInstance takenPotion)
        {
            if (!IsOccupied)
            {
                takenPotion = null;
                return false;
            }

            takenPotion = StoredInstance;
            Clear();
            return true;
        }

        public void Clear()
        {
            StoredInstance = null;
            UpdateVisual();
        }
        #endregion

        #region Visual Update
        private void UpdateVisual()
        {
            if (iconRenderer == null || frameRenderer == null) return;

            bool occupied = StoredPotion != null;

            // --- กรอบของช่อง ---
            Sprite customFrame = occupied ? (filledSlotSprite != null ? filledSlotSprite : emptySlotSprite) : emptySlotSprite;
            if (customFrame != null)
            {
                frameRenderer.enabled = true;
                frameRenderer.sprite = customFrame;
                frameRenderer.color = frameColor;
            }
            else if (keepSceneFrame)
            {
                frameRenderer.enabled = true;
                frameRenderer.sprite = sceneFrameSprite;
                frameRenderer.color = sceneFrameColor;
            }
            else if (occupied)
            {
                frameRenderer.enabled = false; // พฤติกรรมเดิม: มียาแล้วไม่โชว์กรอบ
            }
            else
            {
                frameRenderer.enabled = true;
                frameRenderer.sprite = CraftingSprites2D.Square;
                frameRenderer.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }

            // --- รูปยา (ลูกของช่อง ย่อ/ขยับได้โดยไม่กระทบพื้นที่คลิก) ---
            iconRenderer.enabled = occupied;
            iconRenderer.transform.localPosition = iconOffset;
            if (occupied)
            {
                CraftingSprites2D.Apply(iconRenderer, StoredPotion, CraftingSprites2D.ColorFor(StoredPotion), dragScale);
                iconRenderer.transform.localScale *= storedPotionScale; // ปรับขนาดยาตอนอยู่ในช่อง (ตั้งใน Inspector)
            }
        }
        #endregion

        #region Drag Ghost
        /// <summary>ตัวขวดยาชั่วคราวที่ลอยตามเมาส์ตอนลากออกจากช่องพัก</summary>
        private class StagingDragGhost : MonoBehaviour
        {
            private PotionStagingSlot2D origin;
            private PotionInstance potion;

            public void Init(PotionStagingSlot2D from, PotionInstance p)
            {
                origin = from;
                potion = p;
            }

            private bool dropped;

            private void Update()
            {
                if (dropped) return;

                // ไม่ได้กดเมาส์ค้างอยู่แล้ว = ปล่อยแล้ว (กันพลาดเฟรมที่ปล่อยเมาส์ ทำให้ยาติดเมาส์)
                // โปรเจกต์นี้ใช้ Input System ใหม่ ห้ามใช้ Input.GetMouseButton (จะ error)
                bool held = Mouse.current != null && Mouse.current.leftButton.isPressed;
                if (!held)
                {
                    dropped = true;
                    try { Drop(); }
                    catch (System.Exception e)
                    {
                        // เกิด error ระหว่างวาง -> คืนยากลับช่องเดิมเสมอ ไม่ให้ยาหายหรือค้างเมาส์
                        Debug.LogException(e);
                        if (origin != null && !origin.IsOccupied) origin.Place(potion);
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
                transform.position = CraftingSprites2D.MouseWorldPosition();
                Vector2 point = transform.position;
                Collider2D[] hits = Physics2D.OverlapPointAll(point);

                // 1) ปล่อยบนช่องล่าง (ResultSlotDisplay2D) -> ย้ายกลับไปช่องล่าง (ถ้าเต็มจะเด้งกลับช่องเดิม)
                foreach (var display in FindObjectsByType<ResultSlotDisplay2D>(FindObjectsSortMode.None))
                {
                    if (!display.ContainsPoint(point, out int slotIndex)) continue;

                    // วางลงช่องที่ปล่อยเมาส์ ถ้าช่องนั้นมียาอยู่แล้วหรือปล่อยไม่ตรงช่อง -> ลงช่องว่างช่องแรก
                    if (display.Slot.TryAddAt(slotIndex, potion) || display.Slot.TryAdd(potion))
                    {
                        Finish();
                        return;
                    }
                    break; // เต็ม -> ไปเด้งกลับช่องเดิมด้านล่าง
                }

                // 2) ปล่อยบนช่องพักอื่น -> ย้าย/สลับ
                foreach (var h in hits)
                {
                    var other = h.GetComponent<PotionStagingSlot2D>();
                    if (other != null && other != origin)
                    {
                        PotionInstance displaced = other.StoredInstance;
                        other.Place(potion);
                        if (displaced != null) origin.Place(displaced);
                        Finish();
                        return;
                    }
                }

                // 3) ที่อื่น -> เด้งกลับช่องเดิม
                origin.Place(potion);
                Finish();
            }

            private void Finish() => Destroy(gameObject);
        }
        #endregion
    }
}