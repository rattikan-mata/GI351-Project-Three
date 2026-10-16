using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// หม้อต้ม (2D World Space) ทำหน้าที่เป็น Drop Zone (Collider2D IsTrigger)
    /// - DropTray(set) -> PotManager.TryAddTray: ใส่วัตถุดิบ 3 ชิ้นจากถาด
    /// - พอหม้อเต็มและมีสูตรที่ได้ จะเริ่มปรุงอัตโนมัติ (ตั้งเวลาได้ ค่าเริ่มต้น 3 วินาที) ไม่ต้องคลิก
    ///   ระหว่างปรุงมีวงกลมหมุนที่หม้อ พอครบเวลาจะ Craft เอง
    ///   ถ้า Result Slot เต็มตอนปรุงเสร็จ จะรอจนมีที่ว่างแล้วค่อย Craft
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PotStation2D : MonoBehaviour
    {
        [SerializeField] private PotManager potManager;

        [Header("Auto Brew")]
        [SerializeField, Min(0f)] private float brewSeconds = 3f;
        [Tooltip("ขนาดวงกลมหมุนตอนปรุง และตำแหน่งเทียบกับหม้อ")]
        [SerializeField, Min(0.1f)] private float spinnerScale = 1f;
        [SerializeField] private Vector3 spinnerOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private int spinnerSortingOrder = 20;

        // ของเดิม: ไอคอนวัตถุดิบลอยเหนือหม้อ (Pot Contents) เลิกใช้แล้ว เก็บฟิลด์ไว้เพื่อซ่อนของที่เคยตั้งไว้ในซีน
        [HideInInspector, SerializeField] private SpriteRenderer[] contentDisplays = new SpriteRenderer[PotManager.PotCapacity];

        private SpriteRenderer spinner;
        private Coroutine brewRoutine;

        public bool IsBrewing => brewRoutine != null;

        private void Awake()
        {
            var go = new GameObject("BrewSpinner");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = spinnerOffset;
            go.transform.localScale = Vector3.one * spinnerScale;
            spinner = go.AddComponent<SpriteRenderer>();
            spinner.sprite = IngredientTray2D.RingSprite;
            spinner.sortingOrder = spinnerSortingOrder;
            go.SetActive(false);
        }

        private void OnEnable()
        {
            HideLegacyContentDisplays();
            if (potManager == null) return;
            potManager.OnContentsChanged += HandleContentsChanged;
        }

        private void OnDisable()
        {
            if (potManager != null) potManager.OnContentsChanged -= HandleContentsChanged;
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

        private void Update()
        {
            if (spinner != null && spinner.gameObject.activeSelf)
                spinner.transform.Rotate(0f, 0f, -360f * Time.deltaTime);
        }

        // ---------- Auto brew ----------

        private void HandleContentsChanged()
        {
            if (potManager.IsFull && potManager.CurrentResult != null)
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
            spinner.transform.localRotation = Quaternion.identity;
            spinner.gameObject.SetActive(true);

            yield return new WaitForSeconds(brewSeconds);

            // ปรุงเสร็จ: Craft ให้เอง ถ้า Result Slot เต็มจะรอจนมีที่ว่าง
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

            spinner.gameObject.SetActive(false);
            brewRoutine = null;
        }

        private void StopBrew()
        {
            if (brewRoutine != null)
            {
                StopCoroutine(brewRoutine);
                brewRoutine = null;
            }
            if (spinner != null) spinner.gameObject.SetActive(false);
        }

        private void HideLegacyContentDisplays()
        {
            if (contentDisplays == null) return;
            foreach (var sr in contentDisplays)
                if (sr != null) sr.gameObject.SetActive(false);
        }
    }
}
