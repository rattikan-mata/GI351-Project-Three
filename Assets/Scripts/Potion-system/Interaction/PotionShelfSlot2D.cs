using TMPro;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ช่องขวดยา 1 ช่องบนชั้นวาง: แสดงรูปขวด + จำนวน (xN)
    /// กดค้างแล้วลากขวดไปวางที่ช่องเตรียมยิง (ยาในช่องลดลง 1) ปล่อยที่อื่น = ขวดกลับเข้าชั้น
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PotionShelfSlot2D : MonoBehaviour
    {
        [SerializeField] private PotionShelf shelf;

        [Header("Visual")]
        [Tooltip("ความสูงของรูปขวดในช่อง (หน่วยโลก) รูปจะคงสัดส่วน")]
        [SerializeField, Min(0.05f)] private float iconHeight = 0.7f;
        [SerializeField] private int sortingOrder = 10;
        [Tooltip("ตำแหน่งตัวเลขจำนวน เทียบกับกลางช่อง")]
        [SerializeField] private Vector2 countOffset = new Vector2(0.3f, -0.3f);
        [SerializeField, Min(0.1f)] private float countFontSize = 3f;

        [Header("Drag")]
        [SerializeField] private float dragScale = 0.8f;
        [SerializeField] private int dragSortingOrder = 100;

        private SpriteRenderer iconRenderer;
        private TextMeshPro countLabel;

        public PotionData Potion { get; private set; }
        public int Count { get; private set; }
        public bool IsEmpty => Potion == null || Count <= 0;

        private void Awake()
        {
            if (shelf == null) shelf = GetComponentInParent<PotionShelf>();
            GetComponent<BoxCollider2D>().isTrigger = true;
            EnsureVisuals();
            Refresh();
        }

        /// <summary>ตั้งยาและจำนวนของช่องนี้ (PotionShelf เป็นคนเรียก)</summary>
        public void Set(PotionData potion, int count)
        {
            Potion = count > 0 ? potion : null;
            Count = Mathf.Max(0, count);
            Refresh();
        }

        private void OnMouseDown()
        {
            if (shelf == null || IsEmpty) return;
            if (!shelf.TakeOne(this, out PotionData taken)) return;

            CraftingSfx.Play(CraftingSfx.PotionPick);
            PotionDragGhost.Spawn(new PotionInstance(taken), null, shelf, dragScale, dragSortingOrder);
        }

        private void EnsureVisuals()
        {
            if (iconRenderer == null)
            {
                var go = new GameObject("PotionIcon");
                go.transform.SetParent(transform, false);
                iconRenderer = go.AddComponent<SpriteRenderer>();
                iconRenderer.sortingOrder = sortingOrder;
            }

            if (countLabel == null)
            {
                var go = new GameObject("Count");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = countOffset;
                countLabel = go.AddComponent<TextMeshPro>();
                countLabel.fontSize = countFontSize;
                countLabel.alignment = TextAlignmentOptions.Center;
                countLabel.color = Color.white;
                countLabel.outlineWidth = 0.2f;
                countLabel.outlineColor = Color.black;
                countLabel.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
                countLabel.sortingOrder = sortingOrder + 1;
            }
        }

        private void Refresh()
        {
            if (iconRenderer == null || countLabel == null) return;

            bool show = !IsEmpty;
            iconRenderer.enabled = show;
            countLabel.enabled = show;
            if (!show) return;

            CraftingSprites2D.Apply(iconRenderer, Potion, CraftingSprites2D.ColorFor(Potion), iconHeight);
            if (iconRenderer.sprite != null && Potion.Icon != null)
            {
                float h = iconRenderer.sprite.bounds.size.y;
                if (h > 0.0001f) iconRenderer.transform.localScale = Vector3.one * (iconHeight / h);
            }
            countLabel.text = "x" + Count;
        }
    }
}
