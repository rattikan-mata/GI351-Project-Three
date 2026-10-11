using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// หม้อต้ม (2D World Space) ทำหน้าที่เป็น Drop Zone (Collider2D IsTrigger)
    /// - Drop(material) -> PotManager.TryAddMaterial
    /// - คลิกซ้าย (OnMouseDown) -> PotManager.TryCraft
    /// - แสดงวัตถุดิบในหม้อ (สูงสุด 3 ชิ้น) ผ่าน Child ที่ลอยเหนือหม้อ
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PotStation2D : MonoBehaviour
    {
        [SerializeField] private PotManager potManager;
        [SerializeField] private SpriteRenderer[] contentDisplays = new SpriteRenderer[PotManager.PotCapacity];

        private void OnEnable()
        {
            if (potManager == null) return;
            potManager.OnContentsChanged += RefreshDisplay;
            RefreshDisplay();
        }

        private void OnDisable()
        {
            if (potManager != null) potManager.OnContentsChanged -= RefreshDisplay;
        }

        public AddMaterialResult Drop(RawMaterialData material)
        {
            return potManager.TryAddMaterial(material);
        }

        private void OnMouseDown()
        {
            var result = potManager.TryCraft();
            if (result != CraftResult.Success)
                Debug.Log($"[Pot] Craft ไม่สำเร็จ: {result}");
        }

        private void RefreshDisplay()
        {
            var contents = potManager.Contents;
            for (int i = 0; i < contentDisplays.Length; i++)
            {
                var sr = contentDisplays[i];
                if (sr == null) continue;

                if (i < contents.Count)
                {
                    CraftingSprites2D.Apply(sr, contents[i], CraftingSprites2D.ColorFor(contents[i]), 0.6f);
                    sr.gameObject.SetActive(true);
                }
                else
                {
                    sr.gameObject.SetActive(false);
                }
            }
        }
    }
}
