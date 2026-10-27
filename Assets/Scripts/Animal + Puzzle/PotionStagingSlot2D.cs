using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ช่องเตรียมยิง (ช่องพักยา) ก่อนส่งไปยัง Potion Sequence Dropper
    /// - ลากขวดจากชั้นวาง (PotionShelf) มาวางได้
    /// - คลิกค้างที่ช่องนี้เพื่อหยิบยาขึ้นมาลาก แล้วปล่อยที่ (ดู PotionDragGhost):
    ///     ชั้นวาง        -> คืนเข้าชั้น
    ///     ช่องพักอีกช่อง -> สลับ/ย้ายไปช่องนั้น
    ///     ที่อื่น        -> เด้งกลับช่องเดิม (ยาไม่หาย)
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

        [Tooltip("กรอบของขวดยาตอนอยู่ในช่องนี้ (หน่วยโลก) ขวดทุกแบบถูกย่อให้พอดีกรอบโดยคงสัดส่วน ใช้ค่าเดียวกับชั้นวางเพื่อให้ขนาดเท่ากัน ไม่กระทบขนาดตอนลาก และไม่กระทบพื้นที่คลิก")]
        [SerializeField, Min(0.1f)] private float storedBox = 1.0f;

        [Tooltip("ขยับรูปยาจากกึ่งกลางช่อง (หน่วย local)")]
        [SerializeField] private Vector2 iconOffset = Vector2.zero;
        #endregion

        #region Drag Settings
        [Header("Drag")]
        [Tooltip("กรอบของขวดยาตอนลาก (หน่วยโลก)")]
        [SerializeField] private float dragBox = 1.2f;
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
            CraftingSfx.Play(CraftingSfx.PotionPick);
            PotionDragGhost.Spawn(taken, this, null, dragBox, dragSortingOrder);
        }
        #endregion

        #region Public API
        /// <summary>ใส่ยาลงช่องนี้ (มาจากชั้นวาง / เด้งกลับ / สลับช่อง)</summary>
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

        /// <summary>กะพริบกรอบเป็นสีแดง บอกผู้เล่นว่าช่องนี้ยังต้องใส่ยา (ใช้ตอนกดส่งแต่ยาไม่ครบ)</summary>
        public void FlashEmpty()
        {
            if (frameRenderer == null || !isActiveAndEnabled) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }
        #endregion

        #region Flash
        [Header("Flash (กะพริบตอนส่งยาไม่ครบ)")]
        [SerializeField] private Color flashColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField, Min(1)] private int flashCount = 3;
        [SerializeField, Min(0.02f)] private float flashInterval = 0.12f;
        private Coroutine flashRoutine;

        private System.Collections.IEnumerator FlashRoutine()
        {
            bool wasEnabled = frameRenderer.enabled;
            Color restColor = frameRenderer.color;
            frameRenderer.enabled = true;
            for (int i = 0; i < flashCount; i++)
            {
                frameRenderer.color = flashColor;
                yield return new WaitForSeconds(flashInterval);
                frameRenderer.color = restColor;
                yield return new WaitForSeconds(flashInterval);
            }
            frameRenderer.enabled = wasEnabled;
            flashRoutine = null;
            UpdateVisual(); // คืนกรอบตามสถานะช่องปัจจุบัน (เผื่อมีการวางยาระหว่างกะพริบ)
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
                CraftingSprites2D.FitPotion(iconRenderer, StoredPotion, CraftingSprites2D.ColorFor(StoredPotion), storedBox);
            }
        }
        #endregion
    }
}