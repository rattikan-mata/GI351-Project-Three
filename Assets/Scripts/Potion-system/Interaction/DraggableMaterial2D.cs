using UnityEngine;
using TMPro;
namespace PotionSystem
{
    /// <summary>
    /// วัตถุดิบบนชั้นวาง (2D World Space): ลากด้วยเมาส์ได้
    /// ปล่อยในพื้นที่หม้อ -> TryAddMaterial / ปล่อยนอกหม้อ -> วาร์ปกลับตำแหน่งเดิม
    /// จำนวนคงเหลือดึงจาก SourceInventory
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class DraggableMaterial2D : MonoBehaviour
    {
        [SerializeField] private RawMaterialData material;
        [SerializeField] private SourceInventory sourceInventory;
        [SerializeField] private PotStation2D pot;
        [SerializeField] private TextMeshPro label;

        private SpriteRenderer sr;
        private Collider2D col;
        private Vector3 homePosition;
        private Vector3 grabOffset;
        private int homeSortingOrder;
        private bool dragging;

        public RawMaterialData Material => material;

        public void Init(RawMaterialData data, SourceInventory inventory, PotStation2D potStation)
        {
            material = data;
            sourceInventory = inventory;
            pot = potStation;
        }

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            col.isTrigger = true;
            homePosition = transform.position;
            homeSortingOrder = sr.sortingOrder;
        }

        private void OnEnable()
        {
            if (sourceInventory != null) sourceInventory.OnQuantityChanged += HandleQuantityChanged;
        }

        private void OnDisable()
        {
            if (sourceInventory != null) sourceInventory.OnQuantityChanged -= HandleQuantityChanged;
        }

        private void Start()
        {
            Refresh();
        }

        private void HandleQuantityChanged(RawMaterialData m, int qty)
        {
            if (m == material) Refresh();
        }

        private void Refresh()
        {
            int qty = sourceInventory != null ? sourceInventory.GetQuantity(material) : 0;
            if (label != null) label.text = "x" + qty;
            var c = sr.color;
            c.a = qty > 0 ? 1f : 0.35f;
            sr.color = c;
        }


        private void OnMouseDown()
        {
            if (sourceInventory == null || !sourceInventory.HasStock(material)) return;
            dragging = true;
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

            var hits = Physics2D.OverlapPointAll(CraftingSprites2D.MouseWorldPosition());
            foreach (var hit in hits)
            {
                var zone = hit.GetComponent<PotStation2D>();
                if (zone == null) continue;
                var result = zone.Drop(material);
                if (result != AddMaterialResult.Success)
                    Debug.Log($"[Pot] ใส่ {material.DisplayName} ไม่ได้: {result}");
                break;
            }

            // ปล่อยนอกหม้อ (หรือใส่เสร็จแล้ว) -> วาร์ปกลับที่เดิม (ชั้นวางเป็นแหล่งวัตถุดิบ)
            transform.position = homePosition;
            sr.sortingOrder = homeSortingOrder;
        }
    }
}
