using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    public enum TrayState
    {
        Ready,      // พร้อมใช้ ลากได้
        InPot,      // อยู่ในหม้อ รอ Craft (ล็อก)
        Cooldown    // ใช้แล้ว กำลังคูลดาวน์ (วงกลมหมุน)
    }

    /// <summary>
    /// ถาดเซ็ตยา 1 ถาด (2D World Space): มีวัตถุดิบครบ 3 ชิ้นของสูตรหนึ่ง
    /// ลากถาดไปปล่อยบนหม้อ -> ใส่วัตถุดิบ 3 ชิ้นพร้อมกัน
    /// ปล่อยที่อื่น -> ถาดเลื่อนกลับช่องเดิม
    /// สร้างและจัดตำแหน่งโดย IngredientTrayManager (ภาพทั้งหมดสร้างด้วยโค้ด ไม่ต้องใช้ Prefab)
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class IngredientTray2D : MonoBehaviour
    {
        private const int MaterialCount = PotionBlueprint.RequiredMaterialCount;

        private IngredientTrayManager manager;
        private BoxCollider2D col;
        private SpriteRenderer background;
        private readonly SpriteRenderer[] icons = new SpriteRenderer[MaterialCount];
        private RadialFillRing ring;

        private Vector2 size;
        private float iconScale;
        private int baseOrder;
        private float moveSpeed;

        private Vector3 homePosition;
        private Vector3 grabOffset;
        private bool dragging;
        private float cooldownStartTime;
        private float cooldownEndTime;

        public TrayState State { get; private set; } = TrayState.Ready;
        public PotionBlueprint Blueprint { get; private set; }
        public IReadOnlyList<RawMaterialData> Materials => Blueprint != null ? Blueprint.UseMaterials : null;

        // ---------- Setup ----------

        public void Init(IngredientTrayManager owner, Vector2 traySize, float materialIconScale,
                         int sortingOrder, Sprite backgroundSprite, float slideSpeed,
                         Color ringTrackColor, Color ringFillColor)
        {
            manager = owner;
            size = traySize;
            iconScale = materialIconScale;
            baseOrder = sortingOrder;
            moveSpeed = slideSpeed;

            col.size = size;

            var bgGo = new GameObject("BG");
            bgGo.transform.SetParent(transform, false);
            background = bgGo.AddComponent<SpriteRenderer>();
            background.sprite = backgroundSprite != null ? backgroundSprite : CraftingSprites2D.Square;
            Vector3 b = background.sprite.bounds.size;
            bgGo.transform.localScale = new Vector3(size.x / b.x, size.y / b.y, 1f);
            background.sortingOrder = baseOrder;

            for (int i = 0; i < MaterialCount; i++)
            {
                var go = new GameObject("Item_" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3((i - (MaterialCount - 1) * 0.5f) * (size.x / MaterialCount), 0f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = baseOrder + 1;
                icons[i] = sr;
            }

            // วงโหลดกลางถาด: ชั้นล่างเป็นราง ชั้นบนเติมสีจนเต็มเมื่อคูลดาวน์จบ
            ring = RadialFillRing.Create(transform, Vector3.zero, Mathf.Min(size.x, size.y) * 0.7f,
                                         baseOrder + 3, ringTrackColor, ringFillColor);

            Refresh();
        }

        public void SetBlueprint(PotionBlueprint blueprint)
        {
            Blueprint = blueprint;
            Refresh();
        }

        /// <summary>กำหนดตำแหน่งช่องของถาด (snap = true วาร์ปทันที ไม่งั้นค่อยเลื่อนไป)</summary>
        public void SetHome(Vector3 position, bool snap = false)
        {
            homePosition = position;
            if (snap) transform.position = position;
        }

        public void SetState(TrayState state)
        {
            State = state;
            if (state != TrayState.Ready) CancelDrag();
            if (ring != null)
            {
                if (state == TrayState.Cooldown) ring.Show();
                else ring.Hide();
            }
            ApplyStateVisual();
        }

        /// <summary>
        /// เริ่มคูลดาวน์: วงโหลดเติมจากเวลา startTime จนเต็มที่ endTime (Time.time)
        /// ถาดที่เริ่มทีหลังในชุดเดียวกันจะเริ่มที่ช่วงกลาง ๆ แล้วเต็มพร้อมถาดแรก
        /// </summary>
        public void StartCooldown(float startTime, float endTime)
        {
            cooldownStartTime = startTime;
            cooldownEndTime = Mathf.Max(startTime, endTime);
            SetState(TrayState.Cooldown);
            ring.SetProgress(Mathf.InverseLerp(cooldownStartTime, cooldownEndTime, Time.time));
        }

        // ---------- Unity ----------

        private void Awake()
        {
            col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            homePosition = transform.position;
        }

        private void OnDisable()
        {
            CancelDrag();
        }

        private void Update()
        {
            if (!dragging && transform.position != homePosition)
                transform.position = Vector3.MoveTowards(transform.position, homePosition, moveSpeed * Time.deltaTime);

            if (State == TrayState.Cooldown)
            {
                ring.SetProgress(Mathf.InverseLerp(cooldownStartTime, cooldownEndTime, Time.time));
                if (Time.time >= cooldownEndTime && manager != null)
                    manager.HandleCooldownFinished(this);
            }
        }

        // ---------- Drag ----------

        private void OnMouseDown()
        {
            if (State != TrayState.Ready) return;
            dragging = true;
            col.enabled = false; // กันตัวเองบังตอนตรวจว่าปล่อยโดนหม้อหรือไม่
            grabOffset = transform.position - CraftingSprites2D.MouseWorldPosition();
            SetOrderOffset(50);
        }

        private void OnMouseDrag()
        {
            if (!dragging) return;
            transform.position = CraftingSprites2D.MouseWorldPosition() + grabOffset;
        }

        private void OnMouseUp()
        {
            if (!dragging) return;
            CancelDrag();

            var hits = Physics2D.OverlapPointAll(CraftingSprites2D.MouseWorldPosition());
            foreach (var hit in hits)
            {
                var pot = hit.GetComponent<PotStation2D>();
                if (pot == null) continue;
                if (manager != null) manager.TryUseTray(this, pot);
                break;
            }
            // ไม่ว่าสำเร็จหรือไม่ ถาดจะเลื่อนกลับช่อง (Update) เอง
        }

        private void CancelDrag()
        {
            if (!dragging && col != null && col.enabled) return;
            dragging = false;
            if (col != null) col.enabled = true;
            SetOrderOffset(0);
        }

        private void SetOrderOffset(int offset)
        {
            if (background == null) return;
            background.sortingOrder = baseOrder + offset;
            for (int i = 0; i < icons.Length; i++)
                if (icons[i] != null) icons[i].sortingOrder = baseOrder + offset + 1;
            if (ring != null) ring.SetSortingOrder(baseOrder + offset + 3);
        }

        // ---------- Visual ----------

        private void Refresh()
        {
            var mats = Materials;
            for (int i = 0; i < icons.Length; i++)
            {
                var sr = icons[i];
                if (sr == null) continue;

                var m = (mats != null && i < mats.Count) ? mats[i] : null;
                if (m == null)
                {
                    sr.gameObject.SetActive(false);
                    continue;
                }

                sr.gameObject.SetActive(true);
                CraftingSprites2D.Apply(sr, m, CraftingSprites2D.ColorFor(m), iconScale);
                if (m.Icon != null) sr.transform.localScale = Vector3.one * iconScale;
            }
            ApplyStateVisual();
        }

        private void ApplyStateVisual()
        {
            if (background == null) return;

            Color bg;
            float iconAlpha;
            switch (State)
            {
                case TrayState.InPot:
                    bg = new Color(0.75f, 0.7f, 0.5f);
                    iconAlpha = 0.4f;
                    break;
                case TrayState.Cooldown:
                    bg = new Color(0.4f, 0.4f, 0.4f);
                    iconAlpha = 0.3f;
                    break;
                default:
                    bg = new Color(0.95f, 0.93f, 0.85f);
                    iconAlpha = 1f;
                    break;
            }

            background.color = bg;
            foreach (var sr in icons)
            {
                if (sr == null) continue;
                var c = sr.color;
                c.a = iconAlpha;
                sr.color = c;
            }
        }
    }
}
