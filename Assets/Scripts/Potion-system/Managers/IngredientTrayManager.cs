using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ตัวสร้าง/จัดการถาดเซ็ตยา (แทน SourceInventory + วัตถุดิบเดี่ยว)
    /// - ตั้งใน Inspector ได้ว่าด่านนี้มีสูตรไหนบ้าง (Recipe Pool) พร้อมน้ำหนักสุ่ม / จำนวนขั้นต่ำ / สูงสุด
    /// - สุ่มถาด 9 ถาดตอนเริ่มด่าน เรียงเป็นแถว
    /// - ลากถาดไปปล่อยที่หม้อ -> ใส่วัตถุดิบ 3 ชิ้น และถาดเริ่มรีเฟรชทันที: ไปต่อท้ายแถว (ช่องสุดท้าย)
    ///   ขึ้นวงกลมหมุนคูลดาวน์ ถาดอื่นเลื่อนมาข้างหน้า
    /// - ถาดที่คูลดาวน์อยู่พร้อมกันจะเสร็จพร้อมกัน ตามเวลาของถาดแรก พอครบเวลาถาดพร้อมใช้อีกครั้ง
    /// </summary>
    public class IngredientTrayManager : MonoBehaviour
    {
        [Serializable]
        public class RecipeEntry
        {
            public PotionBlueprint blueprint;
            [Tooltip("น้ำหนักการสุ่ม (ยิ่งมากยิ่งออกบ่อย) 0 = ไม่สุ่มเลย ใช้เฉพาะจำนวนขั้นต่ำ")]
            [Min(0f)] public float weight = 1f;
            [Tooltip("จำนวนถาดขั้นต่ำของสูตรนี้ที่ต้องมีบนแถวเสมอ (กันด่านแก้ไม่ได้)")]
            [Min(0)] public int minCount = 0;
            [Tooltip("จำนวนถาดสูงสุดของสูตรนี้บนแถว 0 = ไม่จำกัด")]
            [Min(0)] public int maxCount = 0;
        }

        [Header("References")]
        [SerializeField] private PotManager potManager;
        [Tooltip("(ไม่บังคับ) ใช้ตรวจเตือนว่า Blueprint ใน Pool อยู่ใน Recipe Book หรือไม่")]
        [SerializeField] private PotionRecipeBook recipeBook;

        [Header("Level Config: สูตรที่ด่านนี้ใช้")]
        [SerializeField] private List<RecipeEntry> recipePool = new List<RecipeEntry>();
        [SerializeField, Min(1)] private int trayCount = 9;
        [Tooltip("เปิด = ใช้ seed คงที่ (สุ่มได้ผลเดิมทุกครั้ง ไว้เทสต์)")]
        [SerializeField] private bool useSeed = false;
        [SerializeField] private int seed = 12345;

        [Header("Cooldown")]
        [SerializeField, Min(0f)] private float cooldownSeconds = 8f;
        [Tooltip("วงโหลดตอนคูลดาวน์: สีราง (ชั้นล่าง) และสีที่ค่อย ๆ เติม (ชั้นบน)")]
        [SerializeField] private Color ringTrackColor = new Color(0.25f, 0.25f, 0.25f, 0.85f);
        [SerializeField] private Color ringFillColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        [Tooltip("เปิด = พอคูลดาวน์จบ ถาดสุ่มสูตรใหม่จาก Pool / ปิด = ได้สูตรเดิม")]
        [SerializeField] private bool rerollAfterCooldown = true;

        [Header("Layout")]
        [Tooltip("จุดของช่องที่ 1 (ว่างไว้ = ตำแหน่งของ GameObject นี้)")]
        [SerializeField] private Transform rowOrigin;
        [Tooltip("จำนวนถาดต่อแถว (3 = เรียง 3x3: 1 2 3 / 4 5 6 / 7 8 9)")]
        [SerializeField, Min(1)] private int columns = 3;
        [Tooltip("ทิศที่ถาดเรียงต่อกันในแถวเดียวกัน (ซ้ายไปขวา)")]
        [SerializeField] private Vector2 rowDirection = Vector2.right;
        [Tooltip("ทิศที่ขึ้นแถวใหม่ (ลงล่าง)")]
        [SerializeField] private Vector2 nextRowDirection = Vector2.down;
        [Tooltip("ระยะห่างระหว่างถาดในแถวเดียวกัน")]
        [SerializeField, Min(0.1f)] private float slotSpacing = 1.8f;
        [Tooltip("ระยะห่างระหว่างแถว")]
        [SerializeField, Min(0.1f)] private float rowSpacing = 1f;
        [SerializeField] private Vector2 traySize = new Vector2(1.6f, 0.8f);
        [Tooltip("ขนาดไอคอนวัตถุดิบบนถาด (ปรับตามขนาดรูปของคุณ)")]
        [SerializeField, Min(0.01f)] private float materialIconScale = 0.5f;
        [SerializeField, Min(0.1f)] private float slideSpeed = 12f;
        [SerializeField] private int sortingOrder = 5;
        [Tooltip("(ไม่บังคับ) รูปพื้นหลังถาด ว่างไว้ = สี่เหลี่ยมเรียบ (ใช้เมื่อสูตรนั้นไม่มีรูปใน Tray Sprites By Kind)")]
        [SerializeField] private Sprite traySprite;

        [Serializable]
        public class TraySpriteEntry
        {
            public PotionKind kind;
            public Sprite sprite;
        }

        [Header("Tray Art: รูปถาดตามชนิดยาที่สูตรทำออกมา")]
        [Tooltip("ถาดของสูตรที่ทำยาชนิดนี้ จะใช้รูปนี้ (เปลี่ยนตามสูตรทุกครั้งที่ถาดสุ่มสูตรใหม่) รูปจะคงสัดส่วน โดยกว้างเท่า Tray Size X")]
        [SerializeField] private List<TraySpriteEntry> traySprites = new List<TraySpriteEntry>();

        [Tooltip("ระยะห่างระหว่างไอคอนวัตถุดิบ 3 ชิ้น เป็นสัดส่วนของความกว้างถาด (ใช้กับรูปใน Tray Sprites By Kind)")]
        [SerializeField, Range(0.05f, 0.5f)] private float iconSpacingRatio = 0.26f;
        [Tooltip("ขยับกลุ่มไอคอนจากกึ่งกลางถาด เป็นสัดส่วนของขนาดถาด (x: ซ้าย-ขวา, y: ขึ้น-ลง)")]
        [SerializeField] private Vector2 iconOffsetRatio = new Vector2(-0.08f, 0.55f);
        [Tooltip("ไอคอนวัตถุดิบอยู่หลังรูปถาด และถูกตัดด้วย Sprite Mask ให้เห็นเฉพาะในกรอบที่กว้างเท่าตัวถาด ก้นกรอบตรงก้นถาด\n" +
                 "ค่านี้คือความสูงของกรอบส่วนที่เลยขอบบนถาดขึ้นไป เป็นสัดส่วนของความสูงถาด (ไอคอนโผล่พ้นถาดได้เท่านี้)")]
        [SerializeField, Min(0f)] private float iconVisibleAboveRatio = 1f;

        [Header("Aura: ออร่าเรืองหลังชุดยา")]
        [Tooltip("ออร่าหลังถาดของสูตรที่ทำยาผสม (Skyward / Havoc / Surge) ว่าง = ไม่มีออร่า")]
        [SerializeField] private Sprite auraComboSprite;
        [Tooltip("ออร่าหลังถาดของสูตรที่ทำยาพื้นฐาน (Red / Blue / Green) ว่าง = ไม่มีออร่า")]
        [SerializeField] private Sprite auraBasicSprite;
        [Tooltip("ความกว้างของออร่า เป็นสัดส่วนของความกว้างถาด")]
        [SerializeField, Min(0.1f)] private float auraWidthRatio = 1.4f;
        [Tooltip("ขยับออร่าจากกึ่งกลางถาด เป็นสัดส่วนของขนาดถาด (x: ซ้าย-ขวา, y: ขึ้น-ลง)")]
        [SerializeField] private Vector2 auraOffsetRatio = new Vector2(0f, 0.3f);

        public float AuraWidthRatio => auraWidthRatio;
        public Vector2 AuraOffsetRatio => auraOffsetRatio;

        /// <summary>ออร่าของสูตรนี้ (ยาผสม = ออร่าสีฟ้า / ยาพื้นฐาน = ออร่าสีเทา) คืน null ถ้าไม่ได้ตั้งไว้</summary>
        public Sprite GetAuraSprite(PotionBlueprint blueprint)
        {
            if (blueprint == null || blueprint.ResultPotion == null) return null;
            switch (blueprint.ResultPotion.Kind)
            {
                case PotionKind.Skyward:
                case PotionKind.Havoc:
                case PotionKind.Surge:
                    return auraComboSprite;
                default:
                    return auraBasicSprite;
            }
        }

        public float IconSpacingRatio => iconSpacingRatio;
        public Vector2 IconOffsetRatio => iconOffsetRatio;
        public float IconVisibleAboveRatio => iconVisibleAboveRatio;

        /// <summary>รูปถาดของสูตรนี้ (ตามชนิดยาที่ได้) คืน null ถ้าไม่ได้ตั้งไว้</summary>
        public Sprite GetTraySprite(PotionBlueprint blueprint)
        {
            if (blueprint == null || blueprint.ResultPotion == null) return null;
            var kind = blueprint.ResultPotion.Kind;
            foreach (var e in traySprites)
                if (e != null && e.kind == kind && e.sprite != null) return e.sprite;
            return null;
        }

        private System.Random rng;
        private readonly List<IngredientTray2D> row = new List<IngredientTray2D>();
        private float batchCooldownStart;
        private float batchCooldownEnd; // เวลาที่คูลดาวน์ชุดปัจจุบันจบ (ถาดที่เริ่มทีหลังจบพร้อมกัน)

        // ---------- Unity ----------

        private void Start()
        {
            if (potManager != null && !potManager.UseIngredientTrays)
                Debug.LogWarning("[Tray] PotManager ปิด Use Ingredient Trays อยู่ ถาดจะใส่หม้อไม่ได้", this);

            FillPoolFromRecipeBookIfEmpty();
            ValidatePool();
            BuildRow();
        }

        private void OnValidate()
        {
            trayCount = Mathf.Max(1, trayCount);
            int minSum = 0;
            foreach (var e in recipePool)
                if (e != null && e.blueprint != null) minSum += e.minCount;
            if (minSum > trayCount)
                Debug.LogWarning($"[Tray] ผลรวม Min Count ({minSum}) มากกว่าจำนวนถาด ({trayCount})", this);
        }

        // ---------- Build ----------

        /// <summary>สร้างแถวถาดใหม่ทั้งหมด (เรียกจากเมนู ... ของคอมโพเนนต์ ตอนรันอยู่ก็ได้)</summary>
        [ContextMenu("Regenerate Trays")]
        public void BuildRow()
        {
            foreach (var t in row)
                if (t != null) Destroy(t.gameObject);
            row.Clear();
            batchCooldownStart = 0f;
            batchCooldownEnd = 0f;

            rng = useSeed ? new System.Random(seed) : new System.Random();

            var recipes = GenerateInitialRecipes();
            for (int i = 0; i < recipes.Count; i++)
            {
                var go = new GameObject("Tray_" + (i + 1));
                go.transform.SetParent(transform, false);
                var tray = go.AddComponent<IngredientTray2D>();
                tray.Init(this, traySize, materialIconScale, sortingOrder, traySprite, slideSpeed, ringTrackColor, ringFillColor);
                tray.SetBlueprint(recipes[i]);
                row.Add(tray);
            }
            UpdateHomes(snap: true);
        }

        private List<PotionBlueprint> GenerateInitialRecipes()
        {
            var result = new List<PotionBlueprint>(trayCount);

            // 1) จำนวนขั้นต่ำ
            foreach (var e in recipePool)
            {
                if (e == null || e.blueprint == null) continue;
                for (int k = 0; k < e.minCount && result.Count < trayCount; k++)
                    result.Add(e.blueprint);
            }

            // 2) ที่เหลือสุ่มตามน้ำหนัก (เคารพ Max Count ก่อน ถ้าไม่ได้ค่อยปล่อย)
            while (result.Count < trayCount)
            {
                var pick = PickWeighted(result, respectMax: true) ?? PickWeighted(result, respectMax: false);
                if (pick == null)
                {
                    Debug.LogError("[Tray] Recipe Pool ว่าง หรือทุกสูตรน้ำหนักเป็น 0 สร้างถาดไม่ครบ", this);
                    break;
                }
                result.Add(pick);
            }

            Shuffle(result);
            return result;
        }

        private PotionBlueprint PickWeighted(List<PotionBlueprint> current, bool respectMax)
        {
            float total = 0f;
            foreach (var e in recipePool)
                if (IsCandidate(e, current, respectMax)) total += e.weight;
            if (total <= 0f) return null;

            float roll = (float)(rng.NextDouble() * total);
            foreach (var e in recipePool)
            {
                if (!IsCandidate(e, current, respectMax)) continue;
                roll -= e.weight;
                if (roll <= 0f) return e.blueprint;
            }
            return null;
        }

        private static bool IsCandidate(RecipeEntry e, List<PotionBlueprint> current, bool respectMax)
        {
            if (e == null || e.blueprint == null || e.weight <= 0f) return false;
            if (respectMax && e.maxCount > 0 && Count(current, e.blueprint) >= e.maxCount) return false;
            return true;
        }

        private static int Count(List<PotionBlueprint> list, PotionBlueprint bp)
        {
            int n = 0;
            foreach (var b in list) if (b == bp) n++;
            return n;
        }

        private void Shuffle(List<PotionBlueprint> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ---------- Layout ----------

        private Vector3 SlotPosition(int index)
        {
            Vector3 origin = rowOrigin != null ? rowOrigin.position : transform.position;
            Vector2 across = rowDirection.sqrMagnitude > 0f ? rowDirection.normalized : Vector2.right;
            Vector2 down = nextRowDirection.sqrMagnitude > 0f ? nextRowDirection.normalized : Vector2.down;
            int cols = Mathf.Max(1, columns);
            int col = index % cols;
            int line = index / cols;
            return origin + (Vector3)(across * (slotSpacing * col) + down * (rowSpacing * line));
        }

        private void UpdateHomes(bool snap = false)
        {
            for (int i = 0; i < row.Count; i++)
                row[i].SetHome(SlotPosition(i), snap);
        }

        // ---------- Use / Cooldown ----------

        /// <summary>
        /// เรียกจากถาดตอนผู้เล่นปล่อยถาดบนหม้อ คืน true ถ้าใส่หม้อสำเร็จ
        /// ถาดเริ่มรีเฟรช (ไปต่อท้ายแถว + คูลดาวน์) ทันทีที่ใส่ ไม่ต้องรอปรุงเสร็จ
        /// </summary>
        public bool TryUseTray(IngredientTray2D tray, PotStation2D pot)
        {
            if (tray == null || pot == null || tray.State != TrayState.Ready) return false;

            var result = pot.DropTray(tray.Materials);
            if (result != AddMaterialResult.Success)
            {
                Debug.Log($"[Tray] ใส่ถาดลงหม้อไม่ได้: {result}");
                return false;
            }

            row.Remove(tray);
            row.Add(tray); // ช่องสุดท้าย ถาดอื่นเลื่อนมาข้างหน้า
            UpdateHomes();
            BeginCooldown(tray);
            return true;
        }

        // คูลดาวน์พร้อมกัน: ถ้ามีถาดกำลังคูลดาวน์อยู่ ถาดใหม่จบพร้อมถาดแรก ไม่งั้นเริ่มชุดใหม่
        private void BeginCooldown(IngredientTray2D tray)
        {
            float now = Time.time;
            if (batchCooldownEnd <= now)
            {
                batchCooldownStart = now;
                batchCooldownEnd = now + cooldownSeconds;
            }
            tray.StartCooldown(batchCooldownStart, batchCooldownEnd);
        }

        /// <summary>เรียกจากถาดตอนคูลดาวน์จบ</summary>
        public void HandleCooldownFinished(IngredientTray2D tray)
        {
            if (tray == null || tray.State != TrayState.Cooldown) return;

            if (rerollAfterCooldown)
                tray.SetBlueprint(PickReplacement(tray));
            tray.SetState(TrayState.Ready);
        }

        private PotionBlueprint PickReplacement(IngredientTray2D tray)
        {
            var others = new List<PotionBlueprint>();
            foreach (var t in row)
                if (t != tray && t.Blueprint != null) others.Add(t.Blueprint);

            // ถ้าสูตรไหนตกต่ำกว่า Min Count บนแถว ให้ถาดนี้กลายเป็นสูตรนั้นก่อน
            foreach (var e in recipePool)
            {
                if (e == null || e.blueprint == null) continue;
                if (Count(others, e.blueprint) < e.minCount) return e.blueprint;
            }

            return PickWeighted(others, respectMax: true)
                   ?? PickWeighted(others, respectMax: false)
                   ?? tray.Blueprint;
        }

        // Recipe Pool ว่าง: ใช้ทุกสูตรใน Recipe Book (น้ำหนักเท่ากัน) แทน ไม่งั้นจะไม่มีถาดเลย
        private void FillPoolFromRecipeBookIfEmpty()
        {
            foreach (var e in recipePool)
                if (e != null && e.blueprint != null) return;

            if (recipeBook == null) return;

            recipePool.Clear();
            foreach (var b in recipeBook.Blueprints)
            {
                if (b == null) continue;
                recipePool.Add(new RecipeEntry { blueprint = b, weight = 1f });
            }
            Debug.LogWarning("[Tray] Recipe Pool ว่าง จึงใช้ทุกสูตรจาก Recipe Book แทน (ตั้ง Recipe Pool ใน Inspector เพื่อกำหนดเอง)", this);
        }

        // ---------- Validation ----------

        private void ValidatePool()
        {
            if (recipePool.Count == 0)
                Debug.LogWarning("[Tray] Recipe Pool ว่าง ใส่ Blueprint อย่างน้อย 1 สูตร", this);
            if (recipeBook == null) return;

            foreach (var e in recipePool)
            {
                if (e == null || e.blueprint == null) continue;
                bool found = false;
                foreach (var b in recipeBook.Blueprints)
                    if (b == e.blueprint) { found = true; break; }
                if (!found)
                    Debug.LogWarning($"[Tray] {e.blueprint.name} ไม่อยู่ใน Recipe Book ปรุงแล้วจะได้ยาผิด", this);
            }
        }
    }
}
