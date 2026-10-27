using System;
using System.Collections.Generic;
using System.Reflection;
using PotionSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Sprites;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// ขอบสีเวลาเมาส์ชี้ของที่โต้ตอบได้ (ขอบตามรูปทรงสไปรต์จริง / พื้นที่ชี้ตาม Collider2D)
///
/// - ไม่ต้องตั้งค่าอะไรก็ใช้ได้: ถ้าไม่มีตัวนี้ในฉาก จะสร้างให้เองตอนเริ่มเกม (ค่าเริ่มต้นขอบสีขาว)
///   ถ้าอยากปรับสี/ความหนา: สร้าง GameObject ว่างในฉากแล้วใส่สคริปต์นี้ ปรับใน Inspector (ปรับตอนเล่นอยู่ได้ด้วย)
/// - ของที่ได้ขอบ: ขวดยา/วัตถุดิบที่ลากได้, ช่องพักที่มียา, หม้อ, แมว, และของอะไรก็ตามที่มี OnMouseDown/Drag/Up
///   (ของใหม่ก็ได้ขอบเอง) หรือแปะ HoverOutlineTarget เพิ่มได้
/// - ตอนลากของ: ของที่ลากไม่มีขอบ ส่วนที่รับ (หม้อ/ช่องพัก/ถังขยะ) ที่เมาส์ลากไปทับจะมีขอบ
/// </summary>
public class HoverOutlineManager : MonoBehaviour
{
    #region Settings
    [Header("Outline (ตั้งค่าขอบ ใช้ร่วมกันทุกอย่าง)")]
    [SerializeField] private bool outlineEnabled = true;

    [Tooltip("สีขอบ")]
    [SerializeField] private Color outlineColor = Color.white;

    [Tooltip("ความหนาขอบรวม (หน่วย Unity / world) ขอบคร่อมเส้นขอบรูป ครึ่งหนึ่งในรูป ครึ่งหนึ่งนอกรูป")]
    [SerializeField, Min(0f)] private float outlineWidth = 0.035f;

    [Header("Detection")]
    [Tooltip("Layer ของ Collider ที่ตรวจเมาส์ (ค่าเริ่มต้น = ทุก Layer)")]
    [SerializeField] private LayerMask hoverLayers = ~0;

    [Tooltip("ไม่ให้มีขอบตอนเมาส์อยู่เหนือ UI (Canvas)")]
    [SerializeField] private bool ignoreWhenOverUI = true;

    [Tooltip("กล้องที่แปลงตำแหน่งเมาส์ (ว่างไว้ = Camera.main)")]
    [SerializeField] private Camera worldCamera;

    [Header("Advanced")]
    [Tooltip("Shader ขอบ ว่างไว้ = โหลด Resources/HoverOutline/SpriteHoverOutline ให้เอง")]
    [SerializeField] private Shader outlineShader;
    #endregion

    #region Types
    private enum DragKind { None, Potion, Other }

    private struct Outlined
    {
        public SpriteRenderer renderer;
        public Material original;
    }
    #endregion

    #region State
    private static readonly int UVRectId = Shader.PropertyToID("_UVRect");
    private static readonly int OutlineUVId = Shader.PropertyToID("_OutlineUV");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly Dictionary<Type, bool> mouseMessageCache = new Dictionary<Type, bool>();

    private Material outlineMaterial;
    private MaterialPropertyBlock block;
    private readonly List<Outlined> active = new List<Outlined>();
    private readonly List<SpriteRenderer> rendererBuffer = new List<SpriteRenderer>();
    private GameObject currentOwner;

    private bool dragMode;
    private DragKind dragKind = DragKind.None;
    private DragKind lastHoverKind = DragKind.None;
    private bool wasPressed;
    #endregion

    #region Auto Create
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<HoverOutlineManager>() != null) return;
        new GameObject("HoverOutlineManager (auto)").AddComponent<HoverOutlineManager>();
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        block = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        EnsureMaterial();
    }

    private void OnDisable()
    {
        Restore();
        dragMode = false;
    }

    private void OnDestroy()
    {
        if (outlineMaterial != null) Destroy(outlineMaterial);
    }

    private void Update()
    {
        if (!outlineEnabled || outlineMaterial == null || !TryGetPointer(out Vector2 screen, out bool pressed))
        {
            Restore();
            dragMode = false;
            wasPressed = false;
            return;
        }

        // เริ่มลาก: กดปุ่มซ้ายตอนเมาส์ชี้ของที่ลากได้ (ใช้ค่าของเฟรมก่อน เพราะ OnMouseDown อาจปิด Collider ไปแล้ว)
        if (!dragMode && pressed && !wasPressed && lastHoverKind != DragKind.None)
        {
            dragMode = true;
            dragKind = lastHoverKind;
        }
        if (dragMode && !pressed) dragMode = false;
        wasPressed = pressed;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        bool overUI = ignoreWhenOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (cam == null || overUI)
        {
            Restore();
            lastHoverKind = DragKind.None;
            return;
        }

        Vector3 sp = new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z));
        Vector2 world = cam.ScreenToWorldPoint(sp);

        // โหมดลาก: หาเฉพาะ "ที่รับ" ที่เมาส์ลากไปทับ (ของที่ลากอยู่ไม่มีขอบ เพราะไม่ใช่ที่รับ)
        // โหมดปกติ: หาของที่โต้ตอบได้ตรงเมาส์ และจำชนิดไว้ใช้ตอนเริ่มลากเฟรมถัดไป
        GameObject owner = Pick(world, dragMode, dragKind, out DragKind hoverKind);
        if (!dragMode) lastHoverKind = owner != null ? hoverKind : DragKind.None;

        if (owner == null)
        {
            Restore();
            return;
        }

        ApplyTo(owner);
    }
    #endregion

    #region Pointer
    private static bool TryGetPointer(out Vector2 screen, out bool pressed)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null)
        {
            screen = Vector2.zero;
            pressed = false;
            return false;
        }
        screen = mouse.position.ReadValue();
        pressed = mouse.leftButton.isPressed;
        return true;
#else
        screen = Input.mousePosition;
        pressed = Input.GetMouseButton(0);
        return true;
#endif
    }
    #endregion

    #region Picking
    /// <summary>หาของที่จะมีขอบ ณ จุดที่เมาส์ชี้ (ชั้นบนสุดตาม Sorting) คืน null ถ้าไม่มี</summary>
    private GameObject Pick(Vector2 world, bool dropMode, DragKind kind, out DragKind ownerKind)
    {
        ownerKind = DragKind.None;
        Collider2D[] hits = Physics2D.OverlapPointAll(world, hoverLayers);

        GameObject best = null;
        long bestKey = long.MinValue;
        float bestZ = float.MaxValue;

        foreach (var c in hits)
        {
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;

            if (!Resolve(c, dropMode, kind, out GameObject owner, out DragKind k)) continue;

            // ชั้นบนสุด: Sorting Layer > Order in Layer > ใกล้กล้อง (z น้อยกว่า)
            long key = long.MinValue + 1;
            var main = FindMainRenderer(owner);
            if (main != null)
                key = ((long)SortingLayer.GetLayerValueFromID(main.sortingLayerID) << 32) + main.sortingOrder;
            float z = owner.transform.position.z;

            if (best == null || key > bestKey || (key == bestKey && z < bestZ))
            {
                best = owner;
                bestKey = key;
                bestZ = z;
                ownerKind = k;
            }
        }
        return best;
    }

    private static bool Resolve(Collider2D c, bool dropMode, DragKind kind, out GameObject owner, out DragKind ownerKind)
    {
        owner = null;
        ownerKind = DragKind.None;

        foreach (var mb in c.GetComponentsInParent<MonoBehaviour>())
        {
            if (mb == null) continue;

            bool ok = dropMode ? IsDropTarget(mb, kind) : IsInteractable(mb, out ownerKind);
            if (!ok) continue;

            var marker = mb.GetComponent<HoverOutlineTarget>();
            if (marker != null && marker.DisableOutline) continue;

            owner = mb.gameObject;
            return true;
        }
        return false;
    }

    private static bool IsInteractable(MonoBehaviour mb, out DragKind kind)
    {
        kind = DragKind.None;
        switch (mb)
        {
            case HoverOutlineTarget t:
                if (t.DisableOutline) return false;
                if (t.IsDraggable) kind = DragKind.Other;
                return t.OutlineOnHover;
            case PotionShelfSlot2D shelfSlot:
                if (shelfSlot.IsEmpty) return false; // ช่องว่างคลิกแล้วไม่เกิดอะไร
                kind = DragKind.Potion;
                return true;
            case PotionStagingSlot2D slot:
                if (!slot.IsOccupied) return false; // ช่องว่างคลิกแล้วไม่เกิดอะไร
                kind = DragKind.Potion;
                return true;
            case PotionBrewer _:
                return true;
            case TestAnimal _:
                return false; // สัตว์ในปริศนาไม่ใช่ของที่ผู้เล่นกด (ใส่ HoverOutlineTarget ถ้าอยากให้มีขอบ)
        }
        return HasMouseMessage(mb.GetType());
    }

    private static bool IsDropTarget(MonoBehaviour mb, DragKind kind)
    {
        if (mb is HoverOutlineTarget t) return t.IsDropTarget;

        switch (kind)
        {
            case DragKind.Potion: return mb is PotionStagingSlot2D || mb is PotionShelfSlot2D;
            case DragKind.Other: return mb is PotionStagingSlot2D || mb is PotionShelfSlot2D;
        }
        return false;
    }

    /// <summary>คลาสนี้มี OnMouseDown / OnMouseDrag / OnMouseUp ไหม (ถือว่าคลิกได้) เก็บผลไว้ต่อชนิด</summary>
    private static bool HasMouseMessage(Type type)
    {
        if (mouseMessageCache.TryGetValue(type, out bool cached)) return cached;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        bool found = false;
        for (Type t = type; t != null && t != typeof(MonoBehaviour) && !found; t = t.BaseType)
        {
            found = t.GetMethod("OnMouseDown", flags) != null
                 || t.GetMethod("OnMouseDrag", flags) != null
                 || t.GetMethod("OnMouseUp", flags) != null;
        }
        mouseMessageCache[type] = found;
        return found;
    }
    #endregion

    #region Renderers
    /// <summary>SpriteRenderer หลักของวัตถุ (ใช้จัดลำดับชั้นตอนเลือก)</summary>
    private SpriteRenderer FindMainRenderer(GameObject owner)
    {
        CollectRenderers(owner, rendererBuffer);
        return rendererBuffer.Count > 0 ? rendererBuffer[0] : null;
    }

    private static bool Usable(SpriteRenderer r)
    {
        return r != null && r.enabled && r.sprite != null && r.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// เลือก SpriteRenderer ที่จะมีขอบ: ตามที่ตั้งใน HoverOutlineTarget > ตัวบนวัตถุเอง >
    /// (แมว) ตัวลูกตัวแรก > ตัวลูกที่ใหญ่ที่สุด
    /// </summary>
    private static void CollectRenderers(GameObject owner, List<SpriteRenderer> result)
    {
        result.Clear();

        var marker = owner.GetComponent<HoverOutlineTarget>();
        if (marker != null && marker.Renderers != null && marker.Renderers.Length > 0)
        {
            foreach (var r in marker.Renderers)
                if (Usable(r)) result.Add(r);
            if (result.Count > 0) return;
        }

        var own = owner.GetComponent<SpriteRenderer>();
        if (Usable(own))
        {
            result.Add(own);
            return;
        }

        if (owner.GetComponent<TestAnimal>() != null)
        {
            var first = owner.GetComponentInChildren<SpriteRenderer>();
            if (Usable(first)) result.Add(first);
            return;
        }

        SpriteRenderer largest = null;
        float largestArea = 0f;
        foreach (var r in owner.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!Usable(r)) continue;
            Vector3 size = r.bounds.size;
            float area = size.x * size.y;
            if (largest == null || area > largestArea)
            {
                largest = r;
                largestArea = area;
            }
        }
        if (largest != null) result.Add(largest);
    }
    #endregion

    #region Apply / Restore
    private void EnsureMaterial()
    {
        if (outlineMaterial != null) return;

        Shader shader = outlineShader != null ? outlineShader : Resources.Load<Shader>("HoverOutline/SpriteHoverOutline");
        if (shader == null) shader = Shader.Find("Sprites/Hover Outline");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("[HoverOutline] ไม่เจอ/ใช้ shader ขอบไม่ได้ (Assets/Resources/HoverOutline/SpriteHoverOutline.shader) ระบบขอบจึงปิดอยู่", this);
            return;
        }

        outlineMaterial = new Material(shader) { name = "HoverOutline (runtime)", hideFlags = HideFlags.DontSave };
    }

    private void ApplyTo(GameObject owner)
    {
        outlineMaterial.SetColor(OutlineColorId, outlineColor);

        if (owner != currentOwner)
        {
            Restore();
            currentOwner = owner;

            CollectRenderers(owner, rendererBuffer);
            foreach (var r in rendererBuffer)
            {
                active.Add(new Outlined { renderer = r, original = r.sharedMaterial });
                r.sharedMaterial = outlineMaterial;
            }
        }

        // อัปเดตทุกเฟรม (สไปรต์/สเกลอาจเปลี่ยนระหว่างชี้อยู่)
        foreach (var o in active)
        {
            if (o.renderer != null && o.renderer.sprite != null) UpdateBlock(o.renderer);
        }
    }

    private void UpdateBlock(SpriteRenderer r)
    {
        Sprite s = r.sprite;
        Vector4 uv = DataUtility.GetOuterUV(s); // (minU, minV, maxU, maxV) ของสไปรต์นี้ในเท็กซ์เจอร์/แอตลาส

        // ขนาดสไปรต์จริงใน world (หน่วย Unity) เพื่อให้ขอบหนาเท่ากันทุกอันไม่ว่าสเกลหรือรูปจะใหญ่แค่ไหน
        Vector3 scale = r.transform.lossyScale;
        float worldW = Mathf.Max(0.0001f, (s.rect.width / s.pixelsPerUnit) * Mathf.Abs(scale.x));
        float worldH = Mathf.Max(0.0001f, (s.rect.height / s.pixelsPerUnit) * Mathf.Abs(scale.y));

        float radius = outlineWidth * 0.5f; // ขอบคร่อมเส้นรูป ครึ่งละข้าง
        float du = radius * (uv.z - uv.x) / worldW;
        float dv = radius * (uv.w - uv.y) / worldH;

        r.GetPropertyBlock(block);
        block.SetVector(UVRectId, uv);
        block.SetVector(OutlineUVId, new Vector4(du, dv, 0f, 0f));
        r.SetPropertyBlock(block);
    }

    private void Restore()
    {
        foreach (var o in active)
        {
            if (o.renderer == null) continue; // ถูกทำลายไปแล้ว (เช่น ขวดยาถูกสร้างใหม่)
            o.renderer.SetPropertyBlock(null);
            o.renderer.sharedMaterial = o.original;
        }
        active.Clear();
        currentOwner = null;
    }
    #endregion
}
