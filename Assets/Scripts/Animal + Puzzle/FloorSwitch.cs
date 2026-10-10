using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สวิตช์: มีของมาทับ/แตะ -> สวิตช์ทำงาน -> Door ที่ผูกไว้เปิด
/// เลือกได้ว่าใครกดได้บ้าง (สัตว์ / กล่อง / ก้อนขน) และตัวที่กำลังลอยอยู่จะไม่กด (ตัวเบาเกิน)
/// ต้องมี Collider2D (ตั้ง Is Trigger ให้อัตโนมัติ)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FloorSwitch : MonoBehaviour
{
    #region Who Can Trigger
    [Header("Who Can Trigger (ใครกดสวิตช์ได้)")]
    [SerializeField] private bool triggeredByAnimals = true;
    [SerializeField] private bool triggeredByBoxes = true;
    [SerializeField] private bool triggeredByHairballs = true;

    [Tooltip("ติ๊กไว้ = สัตว์ที่กำลังลอยอยู่ (ตัวอ้วนบินตอนบิน) จะไม่กดสวิตช์")]
    [SerializeField] private bool ignoreAirborneAnimals = true;
    #endregion

    #region Behavior
    [Header("Behavior")]
    [Tooltip("ติ๊กไว้ = กดครั้งเดียวค้างตลอด ไม่ปล่อยแม้ของที่ทับจะออกไปแล้ว")]
    [SerializeField] private bool latchOnce = false;

    [Tooltip("หน่วงกี่วินาทีหลังของที่ทับออกไป ก่อนสวิตช์จะปล่อย (0 = ปล่อยทันที)")]
    [SerializeField] private float releaseDelay = 0f;
    #endregion

    #region Visual
    [Header("Visual (Optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite offSprite;
    [SerializeField] private Sprite onSprite;
    #endregion

    #region State
    public bool IsActive { get; private set; }
    public event Action<FloorSwitch> StateChanged;

    private readonly HashSet<Collider2D> occupants = new HashSet<Collider2D>();
    private readonly List<Collider2D> removeBuffer = new List<Collider2D>();
    private float releaseTimer = 0f;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        ApplyVisual();
    }

    private void OnTriggerEnter2D(Collider2D other) => occupants.Add(other);
    private void OnTriggerExit2D(Collider2D other) => occupants.Remove(other);

    private void Update()
    {
        bool pressed = HasValidOccupant();

        if (pressed)
        {
            releaseTimer = releaseDelay;
            SetActive(true);
        }
        else if (IsActive && !latchOnce)
        {
            releaseTimer -= Time.deltaTime;
            if (releaseTimer <= 0f) SetActive(false);
        }
    }
    #endregion

    #region Logic
    private bool HasValidOccupant()
    {
        removeBuffer.Clear();
        bool found = false;

        foreach (var c in occupants)
        {
            // OnTriggerExit ไม่ถูกเรียกถ้าของถูกลบ/ปิด Collider จึงต้องเก็บกวาดเอง
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
            {
                removeBuffer.Add(c);
                continue;
            }
            if (IsValidPresser(c)) found = true;
        }

        foreach (var c in removeBuffer) occupants.Remove(c);
        return found;
    }

    private bool IsValidPresser(Collider2D c)
    {
        if (triggeredByAnimals)
        {
            var animal = c.GetComponentInParent<TestAnimal>();
            if (animal != null) return !(ignoreAirborneAnimals && animal.IsAirborne);
        }
        if (triggeredByBoxes && c.GetComponentInParent<PushableBox>() != null) return true;
        if (triggeredByHairballs && c.GetComponentInParent<Hairball>() != null) return true;
        return false;
    }

    private void SetActive(bool active)
    {
        if (IsActive == active) return;
        IsActive = active;
        ApplyVisual();
        StateChanged?.Invoke(this);
    }

    private void ApplyVisual()
    {
        if (spriteRenderer == null) return;
        Sprite s = IsActive ? onSprite : offSprite;
        if (s != null) spriteRenderer.sprite = s;
    }
    #endregion
}
