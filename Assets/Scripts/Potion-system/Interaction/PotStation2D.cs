using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// หม้อต้ม (2D World Space) ทำหน้าที่เป็น Drop Zone (Collider2D IsTrigger)
    /// - DropTray(set) -> PotManager.TryAddTray: ใส่วัตถุดิบ 3 ชิ้นจากถาด
    /// - พอหม้อเต็มและมีสูตรที่ได้ จะเริ่มปรุงอัตโนมัติ (ค่าเริ่มต้น 3 วินาที) ไม่ต้องคลิก
    ///   ระหว่างปรุงมีวงโหลดกลางหม้อ ค่อย ๆ เติมสีจนเต็มวงเมื่อปรุงเสร็จ แล้ว Craft เอง
    ///   ถ้า Result Slot เต็มตอนปรุงเสร็จ วงจะค้างเต็มจนมีที่ว่างแล้วค่อย Craft
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PotStation2D : MonoBehaviour
    {
        [SerializeField] private PotManager potManager;

        [Header("Auto Brew")]
        [SerializeField, Min(0f)] private float brewSeconds = 3f;

        [Header("Brew Ring (วงโหลดกลางหม้อ)")]
        [Tooltip("ขนาดวงเทียบกับด้านที่สั้นกว่าของ Collider หม้อ")]
        [SerializeField, Min(0.1f)] private float ringSizeMultiplier = 0.6f;
        [Tooltip("ขยับวงจากกลาง Collider ของหม้อ (0,0 = กลางหม้อพอดี)")]
        [SerializeField] private Vector2 ringOffset = Vector2.zero;
        [SerializeField] private int ringSortingOrder = 20;
        [SerializeField] private Color ringTrackColor = new Color(0.25f, 0.25f, 0.25f, 0.85f);
        [SerializeField] private Color ringFillColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        // ของเดิม: ไอคอนวัตถุดิบลอยเหนือหม้อ (Pot Contents) เลิกใช้แล้ว เก็บฟิลด์ไว้เพื่อซ่อนของที่เคยตั้งไว้ในซีน
        [HideInInspector, SerializeField] private SpriteRenderer[] contentDisplays = new SpriteRenderer[PotManager.PotCapacity];

        private RadialFillRing ring;
        private Coroutine brewRoutine;

        public bool IsBrewing => brewRoutine != null;

        private void Awake()
        {
            var box = GetComponent<BoxCollider2D>();
            Vector2 center = box.offset + ringOffset; // กลางหม้อ (กลาง Collider)
            float diameter = Mathf.Min(box.size.x, box.size.y) * ringSizeMultiplier;

            ring = RadialFillRing.Create(transform, new Vector3(center.x, center.y, 0f), diameter,
                                         ringSortingOrder, ringTrackColor, ringFillColor);
        }

        private void OnEnable()
        {
            HideLegacyContentDisplays();
            if (potManager == null) return;
            potManager.OnResultChanged += HandleResultChanged;
        }

        private void OnDisable()
        {
            if (potManager != null) potManager.OnResultChanged -= HandleResultChanged;
            StopBrew();
        }

        public AddMaterialResult Drop(RawMaterialData material)
        {
            return potManager.TryAddMaterial(material);
        }

        /// <summary>โหมดถาด: ปล่อยถาดลงหม้อ ใส่วัตถุดิบครบ 3 ชิ้น แล้วเริ่มปรุงอัตโนมัติ</summary>
        public AddMaterialResult DropTray(IReadOnlyList<RawMaterialData> set)
        {
            return potManager.TryAddTray(set);
        }

        // ---------- Auto brew ----------

        // OnResultChanged ถูกเรียกหลังประเมินสูตรเสร็จ (ใช้ OnContentsChanged ไม่ได้ เพราะตอนนั้น CurrentResult ยังไม่อัปเดต)
        private void HandleResultChanged(PotionData result)
        {
            if (potManager.IsFull && result != null)
            {
                if (brewRoutine == null) brewRoutine = StartCoroutine(BrewRoutine());
            }
            else
            {
                StopBrew(); // หม้อถูกเทออก/ว่าง
            }
        }

        private IEnumerator BrewRoutine()
        {
            ring.Show();
            ring.SetProgress(0f);
            CraftingSfx.Play(CraftingSfx.PotBrew);

            float elapsed = 0f;
            while (elapsed < brewSeconds)
            {
                elapsed += Time.deltaTime;
                ring.SetProgress(elapsed / brewSeconds);
                yield return null;
            }
            ring.SetProgress(1f);

            // ปรุงเสร็จ: Craft ให้เอง ถ้า Result Slot เต็มจะรอจนมีที่ว่าง (วงค้างเต็ม)
            while (potManager.IsFull && potManager.CurrentResult != null)
            {
                var result = potManager.TryCraft();
                if (result == CraftResult.Success) break;
                if (result != CraftResult.ResultSlotFull)
                {
                    Debug.Log($"[Pot] Craft ไม่สำเร็จ: {result}");
                    break;
                }
                yield return null;
            }

            ring.Hide();
            brewRoutine = null;
        }

        private void StopBrew()
        {
            if (brewRoutine != null)
            {
                StopCoroutine(brewRoutine);
                brewRoutine = null;
            }
            if (ring != null) ring.Hide();
        }

        private void HideLegacyContentDisplays()
        {
            if (contentDisplays == null) return;
            foreach (var sr in contentDisplays)
                if (sr != null) sr.gameObject.SetActive(false);
        }
    }
}
