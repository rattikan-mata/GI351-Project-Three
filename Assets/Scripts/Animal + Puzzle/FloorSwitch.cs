using System;
using PuzzleEngine;
using UnityEngine;

/// <summary>
/// สวิตช์บนพื้น (ทอกเกิล)
/// ตรรกะอยู่ใน PuzzleEngine (ToggleSignal + OccupancyTracker) สคริปต์นี้ทำแค่ 3 อย่าง:
///   1) ฟังการชน  2) กรองว่าใครกดได้  3) อัปเดตภาพ
/// ชื่อฟิลด์เดิมคงไว้ เพื่อให้ฉากและ prefab เดิมยังใช้ได้
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FloorSwitch : MonoBehaviour, ISignal
{
    #region Who Can Trigger
    [Header("Who Can Trigger (ใครกดสวิตช์ได้)")]
    [SerializeField] private bool triggeredByAnimals = true;
    [SerializeField] private bool triggeredByBoxes = true;
    [SerializeField] private bool triggeredByHairballs = true;

    [Tooltip("ติ๊กไว้ = แมวที่กำลังบินอยู่จะไม่กดสวิตช์ (ก้อนขนกดได้ทั้งตอนบินและตอนนอน)")]
    [SerializeField] private bool ignoreAirborneAnimals = true;
    #endregion

    #region Visual
    [Header("Visual (Optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite offSprite;
    [SerializeField] private Sprite onSprite;
    #endregion

    #region State
    private readonly ToggleSignal toggle = new ToggleSignal();
    private readonly OccupancyTracker occupancy = new OccupancyTracker();

    /// <summary>สถานะปัจจุบัน (ISignal)</summary>
    public bool IsOn => toggle.IsOn;

    /// <summary>ชื่อเดิม ยังคงไว้ให้สคริปต์อื่นที่ใช้อยู่</summary>
    public bool IsActive => toggle.IsOn;

    /// <summary>ยิงเมื่อสถานะสลับ (ISignal)</summary>
    public event Action Changed
    {
        add { toggle.Changed += value; }
        remove { toggle.Changed -= value; }
    }

    /// <summary>ยิงเมื่อสถานะสลับ พร้อมส่งตัวเองไปด้วย (ของเดิม เก็บไว้ให้โค้ดเก่าที่ยังอ้างอยู่)</summary>
    public event Action<FloorSwitch> StateChanged;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        occupancy.BodyEntered += toggle.BodyEntered;
        occupancy.BodyLeft += toggle.BodyExited;
        toggle.Changed += OnToggled;

        ApplyVisual();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ล้างของที่หายไปแล้ว (เช่น ก้อนขนที่ถูกทำลาย) ก่อน ไม่งั้นจะนับเป็นคนยืนค้าง
        occupancy.Prune(IsAlive);

        if (!IsAllowed(other)) return;
        occupancy.ColliderEntered(BodyOf(other));
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // ออกได้เสมอ ไม่ต้องเช็กซ้ำว่าใครกดได้ (ตัวที่ไม่เคยนับจะถูกข้ามเอง)
        occupancy.ColliderExited(BodyOf(other));
    }
    #endregion

    #region Logic
    private void OnToggled()
    {
        ApplyVisual();
        StateChanged?.Invoke(this);
    }

    private bool IsAllowed(Collider2D c)
    {
        if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) return false;

        TestAnimal animal = c.GetComponentInParent<TestAnimal>();
        if (animal != null) return triggeredByAnimals && !(ignoreAirborneAnimals && animal.IsAirborne);

        if (c.GetComponentInParent<PushableBox>() != null) return triggeredByBoxes;
        if (c.GetComponentInParent<Hairball>() != null) return triggeredByHairballs;

        return false;
    }

    /// <summary>ตัวที่ใช้นับ = ตัวหลักของวัตถุ (ไม่ใช่ collider) เพื่อให้วัตถุที่มีหลาย collider นับเป็นตัวเดียว</summary>
    private static object BodyOf(Collider2D c)
    {
        if (c == null) return null;

        TestAnimal animal = c.GetComponentInParent<TestAnimal>();
        if (animal != null) return animal;

        PushableBox box = c.GetComponentInParent<PushableBox>();
        if (box != null) return box;

        Hairball ball = c.GetComponentInParent<Hairball>();
        return ball;
    }

    private static bool IsAlive(object body)
    {
        if (body is UnityEngine.Object unityObject) return unityObject != null;
        return true;
    }

    private void ApplyVisual()
    {
        if (spriteRenderer == null) return;
        Sprite s = IsOn ? onSprite : offSprite;
        if (s != null) spriteRenderer.sprite = s;
    }
    #endregion
}
