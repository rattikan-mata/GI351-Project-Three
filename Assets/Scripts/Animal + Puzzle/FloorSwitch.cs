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

    public enum ButtonAnimMode
    {
        [Tooltip("กดแล้วยุบลงแล้วเด้งกลับขึ้นมาทันที (Press & Bounce)")]
        Bounce,
        [Tooltip("ถ้าเปิดสวิตช์ (Active) จะยุบค้างไว้ ถ้าปิดสวิตช์จะเด้งกลับ")]
        StayWhileActive,
        [Tooltip("ยุบค้างเฉพาะตอนที่มีของเหยียบทับอยู่ พอเดินออกจะเด้งกลับขึ้นมา")]
        HoldWhilePressed
    }

    #region Visual
    [Header("Visual (Optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite offSprite;
    [SerializeField] private Sprite onSprite;

    [Header("Button Press Animation (การกดยุบของปุ่ม)")]
    [Tooltip("Transform ของตัวหัวปุ่ม (ปุ่มสีแดง) ที่จะให้ยุบลง (ถ้าเว้นว่างจะค้นหาลูกชื่อ Button / 1button ให้อัตโนมัติ)")]
    [SerializeField] private Transform buttonCap;

    [Tooltip("ระยะที่ปุ่มยุบลงตามแกน Y (หน่วย local) เช่น 0.08 ถึง 0.15")]
    [SerializeField] private float pressDepth = 0.1f;

    [Tooltip("เวลาในการยุบลงและเด้งกลับ (วินาที)")]
    [SerializeField] private float animationDuration = 0.14f;

    [Tooltip("รูปแบบการเคลื่อนไหว:\n- Bounce: กดแล้วยุบลงแล้วเด้งกลับทันที\n- StayWhileActive: ยุบค้างไว้ตอนเปิดสวิตช์\n- HoldWhilePressed: ยุบค้างเฉพาะตอนมีของเหยียบทับ")]
    [SerializeField] private ButtonAnimMode animMode = ButtonAnimMode.Bounce;
    #endregion

    #region State
    public bool IsActive { get; private set; }
    public event Action<FloorSwitch> StateChanged;

    public float RequiredWeight => requiredWeight;
    public float CurrentWeight { get; private set; }

    private readonly HashSet<Collider2D> pressers = new HashSet<Collider2D>();
    private bool weightSatisfied = false;

    private Vector3 initialCapLocalPos;
    private bool initialPosSaved = false;
    private Coroutine animCoroutine;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        EnsureButtonCap();
        ApplyVisual();
    }

    private void EnsureButtonCap()
    {
        if (buttonCap == null)
        {
            buttonCap = transform.Find("Button") ?? transform.Find("ButtonCap") ?? transform.Find("1button") ?? transform.Find("Cap");
        }

        if (buttonCap != null && !initialPosSaved)
        {
            initialCapLocalPos = buttonCap.localPosition;
            initialPosSaved = true;
        }
    }

    private void OnDisable()
    {
        if (animCoroutine != null)
        {
            StopCoroutine(animCoroutine);
            animCoroutine = null;
        }
        if (buttonCap != null && initialPosSaved)
        {
            buttonCap.localPosition = initialCapLocalPos;
        }
    }

    // ทำงานทันทีเมื่อมีวัตถุเดินเข้ามาชน/ทับครั้งแรก (Trigger รอบเดียว)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsValidPresser(other)) return;

        pressers.Add(other);

        if (requiredWeight <= 0f)
        {
            ToggleSwitch();
            if (animMode == ButtonAnimMode.HoldWhilePressed)
            {
                AnimateTo(initialCapLocalPos - new Vector3(0f, pressDepth, 0f));
            }
            return;
        }

        EvaluateWeight();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        pressers.Remove(other);

        if (animMode == ButtonAnimMode.HoldWhilePressed && pressers.Count == 0)
        {
            AnimateTo(initialCapLocalPos);
        }

        if (requiredWeight <= 0f) return;
        EvaluateWeight();
    }

    private void Update()
    {
        if (requiredWeight <= 0f || pressers.Count == 0) return;

        // ก้อนขนที่ถูกทำลาย/สัตว์ที่ลอยขึ้น จะไม่ส่ง TriggerExit เสมอไป จึงตรวจเองทุกเฟรม
        bool changed = pressers.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy || !IsValidPresser(c)) > 0;
        if (changed)
        {
            if (animMode == ButtonAnimMode.HoldWhilePressed && pressers.Count == 0)
            {
                AnimateTo(initialCapLocalPos);
            }
            EvaluateWeight();
        }
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

        if (animMode == ButtonAnimMode.Bounce)
        {
            AnimateBounce();
        }
        else if (animMode == ButtonAnimMode.StayWhileActive)
        {
            Vector3 target = IsActive ? (initialCapLocalPos - new Vector3(0f, pressDepth, 0f)) : initialCapLocalPos;
            AnimateTo(target);
        }

        StateChanged?.Invoke(this);
    }

    private void AnimateBounce()
    {
        if (buttonCap == null) return;
        EnsureButtonCap();
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(BounceRoutine());
    }

    private IEnumerator BounceRoutine()
    {
        Vector3 downPos = initialCapLocalPos - new Vector3(0f, pressDepth, 0f);
        float halfDuration = Mathf.Max(0.02f, animationDuration * 0.5f);

        // 1. ยุบลง (Ease-in punch)
        float elapsed = 0f;
        Vector3 startPos = buttonCap.localPosition;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            buttonCap.localPosition = Vector3.Lerp(startPos, downPos, t * t);
            yield return null;
        }
        buttonCap.localPosition = downPos;

        // 2. เด้งกลับ (Ease-out spring)
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float easeOut = 1f - (1f - t) * (1f - t);
            buttonCap.localPosition = Vector3.Lerp(downPos, initialCapLocalPos, easeOut);
            yield return null;
        }
        buttonCap.localPosition = initialCapLocalPos;
        animCoroutine = null;
    }

    private void AnimateTo(Vector3 targetPos)
    {
        if (buttonCap == null) return;
        EnsureButtonCap();
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(MoveToRoutine(targetPos));
    }

    private IEnumerator MoveToRoutine(Vector3 targetPos)
    {
        float duration = Mathf.Max(0.04f, animationDuration);
        float elapsed = 0f;
        Vector3 startPos = buttonCap.localPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - (1f - t) * (1f - t);
            buttonCap.localPosition = Vector3.Lerp(startPos, targetPos, ease);
            yield return null;
        }
        buttonCap.localPosition = targetPos;
        animCoroutine = null;
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

        if (animMode == ButtonAnimMode.HoldWhilePressed)
        {
            Vector3 target = satisfied ? (initialCapLocalPos - new Vector3(0f, pressDepth, 0f)) : initialCapLocalPos;
            AnimateTo(target);
        }
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