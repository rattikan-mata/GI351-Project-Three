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
        private SpriteRenderer spinner;

        private Vector2 size;
        private float iconScale;
        private int baseOrder;
        private float moveSpeed;

        private Vector3 homePosition;
        private Vector3 grabOffset;
        private bool dragging;
        private float cooldownEndTime;

        public TrayState State { get; private set; } = TrayState.Ready;
        public PotionBlueprint Blueprint { get; private set; }
        public IReadOnlyList<RawMaterialData> Materials => Blueprint != null ? Blueprint.UseMaterials : null;

        // ---------- Setup ----------

        public void Init(IngredientTrayManager owner, Vector2 traySize, float materialIconScale,
                         int sortingOrder, Sprite backgroundSprite, float slideSpeed)
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

            var spGo = new GameObject("CooldownSpinner");
            spGo.transform.SetParent(transform, false);
            spGo.transform.localScale = Vector3.one * (Mathf.Min(size.x, size.y) * 0.6f);
            spinner = spGo.AddComponent<SpriteRenderer>();
            spinner.sprite = RingSprite;
            spinner.sortingOrder = baseOrder + 3;
            spinner.gameObject.SetActive(false);

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
            spinner.gameObject.SetActive(state == TrayState.Cooldown);
            ApplyStateVisual();
        }

        public void StartCooldown(float seconds)
        {
            cooldownEndTime = Time.time + Mathf.Max(0f, seconds);
            spinner.transform.localRotation = Quaternion.identity;
            SetState(TrayState.Cooldown);
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
                spinner.transform.Rotate(0f, 0f, -360f * Time.deltaTime);
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
            if (spinner != null) spinner.sortingOrder = baseOrder + offset + 3;
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

        // ---------- วงกลมหมุน (สร้างด้วยโค้ด ไม่ต้องใช้รูป) ----------

        private static Sprite ringSprite;

        public static Sprite RingSprite
        {
            get
            {
                if (ringSprite != null) return ringSprite;

                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var px = new Color32[n * n];
                float c = (n - 1) * 0.5f;
                float outer = n * 0.5f - 1f;
                float inner = outer * 0.72f;

                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x - c, dy = y - c;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        if (ang < 0f) ang += 360f;

                        bool onRing = d <= outer && d >= inner && ang <= 270f; // เว้นช่องว่าง 90°
                        px[y * n + x] = onRing ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                    }
                }

                tex.SetPixels32(px);
                tex.Apply();
                ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                ringSprite.name = "TrayCooldownRing";
                return ringSprite;
            }
        }
    }
}
