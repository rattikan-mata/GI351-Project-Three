using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ช่องพักยา 1 ใน 3 ช่อง ก่อนส่งไปยัง Potion Sequence Dropper
    /// มี Collider2D เพื่อรับการลากขวดยามาวาง
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class PotionStagingSlot2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private Sprite emptySlotSprite;

        /// <summary>ขวดยาที่เก็บอยู่ (พร้อมวัตถุดิบที่ใช้จริง)</summary>
        public PotionInstance StoredInstance { get; private set; }
        public PotionData StoredPotion => StoredInstance != null ? StoredInstance.Data : null;
        public bool IsOccupied => StoredInstance != null;

        private void Awake()
        {
            if (iconRenderer == null) iconRenderer = GetComponent<SpriteRenderer>();
            GetComponent<BoxCollider2D>().isTrigger = true;
            Clear();
        }

        public bool TryAssignPotion(ResultSlot resultSlot, int resultIndex, PotionInstance potion)
        {
            if (potion == null) return false;

            // หยิบยาออกจาก ResultSlot ต้นทาง
            if (resultSlot != null && !resultSlot.TryTake(resultIndex, out _))
                return false;

            // ถ้าช่องนี้มียาอยู่เดิม คืนยาเก่ากลับ ResultSlot
            if (StoredInstance != null && resultSlot != null)
            {
                resultSlot.TryAdd(StoredInstance);
            }

            StoredInstance = potion;
            UpdateVisual();
            return true;
        }

        public void Clear()
        {
            StoredInstance = null;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (iconRenderer == null) return;

            if (StoredPotion != null)
            {
                CraftingSprites2D.Apply(iconRenderer, StoredPotion, CraftingSprites2D.ColorFor(StoredPotion), 0.8f);
            }
            else
            {
                if (emptySlotSprite != null)
                {
                    iconRenderer.sprite = emptySlotSprite;
                    iconRenderer.color = Color.white;
                    iconRenderer.transform.localScale = Vector3.one;
                }
                else
                {
                    iconRenderer.sprite = CraftingSprites2D.Square;
                    iconRenderer.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                    iconRenderer.transform.localScale = Vector3.one;
                }
            }
        }
    }
}