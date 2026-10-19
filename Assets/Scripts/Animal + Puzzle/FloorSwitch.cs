using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สวิตช์แบบกดรอบเดียว (Toggle): เมื่อมีของที่ถูกต้องเข้ามาแตะ (TriggerEnter) จะสลับสถานะทันที
/// ไม่ต้องรอให้เดินออก และไม่ต้องค้างยืนทับ
/// ถ้าตั้ง Required Weight > 0 จะสลับสถานะตอนที่ "น้ำหนักรวมของที่ทับอยู่" ถึงค่าที่ตั้ง
/// (น้ำหนักมาจาก Mass ของสัตว์ / กล่อง / ก้อนขน)
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

    #region Weight
    [Header("Weight (น้ำหนักที่ต้องใช้)")]
    [Tooltip("0 = กดด้วยอะไรก็ได้ (ทำงานทุกครั้งที่มีของเข้ามาแตะ แบบเดิม)\n" +
             "มากกว่า 0 = น้ำหนักรวมของที่ทับอยู่ต้องถึงค่านี้ ถึงจะสลับสถานะ\n" +
             "เช่น ก้อนขนปกติ Mass 1, ก้อนใหญ่ Mass 3, กล่อง Mass 2")]
    [SerializeField] private float requiredWeight = 0f;
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

    public float RequiredWeight => requiredWeight;
    public float CurrentWeight { get; private set; }

    private readonly HashSet<Collider2D> pressers = new HashSet<Collider2D>();
    private bool weightSatisfied = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        ApplyVisual();
    }

    // ทำงานทันทีเมื่อมีวัตถุเดินเข้ามาชน/ทับครั้งแรก (Trigger รอบเดียว)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsValidPresser(other)) return;

        if (requiredWeight <= 0f)
        {
            ToggleSwitch();
            return;
        }

        pressers.Add(other);
        EvaluateWeight();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (requiredWeight <= 0f) return;
        if (pressers.Remove(other)) EvaluateWeight();
    }

    private void Update()
    {
        if (requiredWeight <= 0f || pressers.Count == 0) return;

        // ก้อนขนที่ถูกทำลาย/สัตว์ที่ลอยขึ้น จะไม่ส่ง TriggerExit เสมอไป จึงตรวจเองทุกเฟรม
        bool changed = pressers.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy || !IsValidPresser(c)) > 0;
        if (changed) EvaluateWeight();
    }
    #endregion

    #region Logic
    /// <summary>กดสวิตช์จากระยะไกล (เช่น เลเซอร์ของแพะ)</summary>
    public void RemoteToggle() => ToggleSwitch();

    private void ToggleSwitch()
    {
        // สลับสถานะเปิด/ปิด (On <-> Off) ทุกครั้งที่ถูกชน
        IsActive = !IsActive;
        ApplyVisual();
        StateChanged?.Invoke(this);
    }

    private void EvaluateWeight()
    {
        var counted = new HashSet<Component>();
        float total = 0f;
        foreach (var c in pressers)
        {
            if (c == null) continue;
            if (TryGetOwner(c, out Component owner, out float w) && counted.Add(owner)) total += w;
        }
        CurrentWeight = total;

        bool satisfied = total >= requiredWeight;
        if (satisfied && !weightSatisfied) ToggleSwitch();
        weightSatisfied = satisfied;
    }

    private bool TryGetOwner(Collider2D c, out Component owner, out float weight)
    {
        owner = null;
        weight = 0f;

        var animal = c.GetComponentInParent<TestAnimal>();
        if (animal != null) { owner = animal; weight = animal.Mass; return true; }

        var box = c.GetComponentInParent<PushableBox>();
        if (box != null) { owner = box; weight = box.Mass; return true; }

        var ball = c.GetComponentInParent<Hairball>();
        if (ball != null) { owner = ball; weight = ball.Mass; return true; }

        return false;
    }

    private bool IsValidPresser(Collider2D c)
    {
        if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) return false;

        if (triggeredByAnimals)
        {
            var animal = c.GetComponentInParent<TestAnimal>();
            if (animal != null) return !(ignoreAirborneAnimals && animal.IsAirborne);
        }
        if (triggeredByBoxes && c.GetComponentInParent<PushableBox>() != null) return true;
        if (triggeredByHairballs && c.GetComponentInParent<Hairball>() != null) return true;

        return false;
    }

    private void ApplyVisual()
    {
        if (spriteRenderer == null) return;
        Sprite s = IsActive ? onSprite : offSprite;
        if (s != null) spriteRenderer.sprite = s;
    }
    #endregion
}