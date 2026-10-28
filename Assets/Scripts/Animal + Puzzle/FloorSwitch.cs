using System;
using System.Collections;
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

    [Tooltip("ติ๊กไว้ = เลเซอร์ (เช่น เลเซอร์ของแพะ) สามารถกดสวิตช์นี้ได้")]
    [SerializeField] private bool triggeredByLasers = true;
    public bool TriggeredByLasers { get => triggeredByLasers; set => triggeredByLasers = value; }

    [Tooltip("ติ๊กไว้ = สัตว์ที่กำลังลอยอยู่ (ตัวอ้วนบินตอนบิน) จะไม่กดสวิตช์")]
    [SerializeField] private bool ignoreAirborneAnimals = true;
    public bool IgnoreAirborneAnimals { get => ignoreAirborneAnimals; set => ignoreAirborneAnimals = value; }
    #endregion

    #region Blocking
    [Header("Blocking (กล่องทับสวิตช์)")]
    [Tooltip("ติ๊กไว้ = ถ้ามีกล่อง (ที่ยังไม่พัง) วางทับสวิตช์นี้อยู่ สวิตช์จะไม่ทำงานเลย ไม่ว่าใครหรืออะไรมากด (รวมถึงสัตว์ที่วาร์ป/บินมาชน) และตัวกล่องเองก็กดไม่ได้\n" +
             "ต้องทำลายกล่องให้พังก่อน สวิตช์ถึงจะกดได้ ไม่ติ๊ก = ทำงานแบบเดิม (ไม่กระทบสวิตช์ตัวอื่น)")]
    [SerializeField] private bool blockedByBox = false;

    [Tooltip("ระยะเผื่อรอบสวิตช์ที่ใช้ตรวจว่ากล่องทับอยู่ (หน่วยโลก)")]
    [SerializeField, Min(0f)] private float blockCheckMargin = 0.05f;

    /// <summary>มีกล่องที่ยังไม่พังทับสวิตช์อยู่และสวิตช์นี้เปิดโหมด Blocked By Box หรือไม่</summary>
    public bool IsBlocked => blockedByBox && HasBoxOnTop();
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
        [Tooltip("ยุบค้างเฉพาะตอนที่มีของเหยียบทับอยู่ พอเดินออกจะเด้งกลับขึ้นมา (แนะนำเป็นค่าเริ่มต้นสำหรับปุ่มเหยียบ)")]
        HoldWhilePressed = 0,
        [Tooltip("กดแล้วยุบลงแล้วเด้งกลับขึ้นมาทันที (Press & Bounce)")]
        Bounce = 1,
        [Tooltip("ถ้าเปิดสวิตช์ (Active) จะยุบค้างไว้ ถ้าปิดสวิตช์จะเด้งกลับ")]
        StayWhileActive = 2
    }

    #region Visual
    [Header("Visual (Optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite offSprite;
    [SerializeField] private Sprite onSprite;

    [Header("Button Press Animation (การกดยุบของปุ่ม)")]
    [Tooltip("Transform ของตัวหัวปุ่ม (ปุ่มสีแดง) ที่จะให้ยุบลง (ถ้าเว้นว่างจะค้นหาลูกชื่อ button / Button / Cap ให้อัตโนมัติ)")]
    [SerializeField] private Transform buttonCap;

    [Tooltip("ระยะที่ปุ่มยุบลงตามแกน Y (หน่วย local) เช่น 0.08 ถึง 0.2")]
    [SerializeField] private float pressDepth = 0.2f;

    [Tooltip("เวลาในการยุบลงและเด้งกลับ (วินาที)")]
    [SerializeField] private float animationDuration = 0.14f;

    [Tooltip("รูปแบบการเคลื่อนไหว:\n- StayWhileActive: กดแล้วยุบค้าง รอถูกกดอีกครั้งถึงเด้งกลับ (ค่าเริ่มต้น เข้ากับสวิตช์แบบสลับเปิด/ปิด)\n- HoldWhilePressed: ยุบค้างตอนมีของเหยียบทับ พอเดินออกจะเด้งกลับ\n- Bounce: ยุบลงแล้วเด้งกลับทันที (ถ้าไม่มีของค้าง)")]
    [SerializeField] private ButtonAnimMode animMode = ButtonAnimMode.StayWhileActive;
    #endregion

    #region Audio
    [Header("Audio (เสียง)")]
    [Tooltip("เสียงตอนกดปุ่มสวิตช์")]
    [SerializeField] private AudioClip pressSound;

    [Range(0f, 1f)]
    [Tooltip("ระดับความดังของเสียงกดสวิตช์ (0 - 1)")]
    [SerializeField] private float pressVolume = 1f;

    [Tooltip("AudioSource สำหรับเล่นเสียง (ถ้าเว้นว่างจะค้นหาจากตัวสวิตช์ให้อัตโนมัติ)")]
    [SerializeField] private AudioSource audioSource;
    #endregion

    #region State
    public bool IsActive { get; private set; }
    public event Action<FloorSwitch> StateChanged;

    public float RequiredWeight => requiredWeight;
    public float CurrentWeight { get; private set; }

    private readonly HashSet<Collider2D> pressers = new HashSet<Collider2D>();
    private bool weightSatisfied = false;
    private float lastToggleTime = -1f;
    private bool exitPending;

    private Vector3 initialCapLocalPos;
    private bool initialPosSaved = false;
    private Coroutine animCoroutine;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        EnsureButtonCap();
        ApplyVisual();
    }

    private void Start()
    {
        EnsureButtonCap();
    }

    private void OnEnable()
    {
        EnsureButtonCap();
    }

    private void EnsureButtonCap()
    {
        if (buttonCap == null)
        {
            buttonCap = transform.Find("button") ?? transform.Find("Button") ?? transform.Find("ButtonCap") ?? transform.Find("1button") ?? transform.Find("Cap");
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
        pressers.Clear();
        exitPending = false;
    }

    // ทำงานทันทีเมื่อมีวัตถุเดินเข้ามาชน/ทับครั้งแรก (Trigger รอบเดียว)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsValidPresser(other)) return;

        bool wasEmpty = pressers.Count == 0;
        pressers.Add(other);

        if (requiredWeight <= 0f)
        {
            // ทำงานเฉพาะตอนที่มีสิ่งของเหยียบเข้ามาใหม่เป็นตัวแรก
            if (wasEmpty)
            {
                ToggleSwitch();
            }
            UpdateCapPosition();
            return;
        }

        EvaluateWeight();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        pressers.Remove(other);

        // อัปเดตปุ่มในเฟรมถัดไปใน Update แทน: ตอนวัตถุถูกปิด (เช่น รีเซ็ตพัซเซิล/หยุดเล่น) ระบบฟิสิกส์ยิง Exit มาในจังหวะที่สตาร์ท Coroutine ไม่ได้
        exitPending = true;
    }

    private void Update()
    {
        if (exitPending)
        {
            exitPending = false;
            if (requiredWeight > 0f) EvaluateWeight();
            else UpdateCapPosition();
        }

        if (pressers.Count == 0) return;

        // ก้อนขนที่ถูกทำลาย/สัตว์ที่ลอยขึ้น จะไม่ส่ง TriggerExit เสมอไป จึงตรวจเองทุกเฟรม
        bool changed = pressers.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy || !IsValidPresser(c)) > 0;
        if (changed)
        {
            if (requiredWeight > 0f)
            {
                EvaluateWeight();
            }
            else
            {
                UpdateCapPosition();
            }
        }
    }
    #endregion

    #region Logic
    /// <summary>กดสวิตช์จากระยะไกล (เช่น เลเซอร์ของแพะ)</summary>
    public void RemoteToggle()
    {
        if (!triggeredByLasers) return;
        ToggleSwitch();
        if (pressers.Count == 0 && animMode != ButtonAnimMode.StayWhileActive)
        {
            AnimateBounce();
        }
    }

    /// <summary>ตรวจว่ามี Collider นี้เหยียบ/ทับสวิตช์อยู่หรือไม่</summary>
    public bool ContainsPresser(Collider2D col) => col != null && pressers.Contains(col);

    /// <summary>สั่งสลับสถานะสวิตช์โดยตรง (เช่น เมื่อสัตว์บินชนสวิตช์)</summary>
    public void ForceToggle()
    {
        ToggleSwitch();
        if (pressers.Count == 0 && animMode != ButtonAnimMode.StayWhileActive)
        {
            AnimateBounce();
        }
    }

    private void ToggleSwitch()
    {
        if (IsBlocked)
        {
            // มีกล่องทับอยู่ ต้องพังกล่องก่อนถึงจะกดได้ แต่ปุ่มยังบุ๋มลงไปแล้วเด้งกลับเหมือนสวิตช์ที่กดไม่ติด
            AnimateBounce();
            return;
        }

        // ชนครั้งเดียวแต่ถูกสั่งซ้ำในจังหวะฟิสิกส์เดียวกัน (เช่น วัวบินสั่ง ForceToggle + สวิตช์เจอ Trigger เอง) ให้นับครั้งเดียว
        if (lastToggleTime == Time.fixedTime) return;
        lastToggleTime = Time.fixedTime;

        // สลับสถานะเปิด/ปิด (On <-> Off) ทุกครั้งที่ถูกชน
        IsActive = !IsActive;
        ApplyVisual();

        UpdateCapPosition();
        PlayPressSound();
        StateChanged?.Invoke(this);
    }

    private void PlayPressSound()
    {
        AudioClip clip = pressSound != null ? pressSound : (AudioManager.Instance != null ? AudioManager.Instance.GetClip("SwitchPress") : null);
        if (clip == null) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXAt(clip, transform.position, pressVolume);
        }
        else if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, pressVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, pressVolume);
        }
    }

    private void UpdateCapPosition()
    {
        if (buttonCap == null) return;
        EnsureButtonCap();

        Vector3 downPos = initialCapLocalPos - new Vector3(0f, pressDepth, 0f);

        if (animMode == ButtonAnimMode.HoldWhilePressed)
        {
            bool isHeldDown = (requiredWeight > 0f) ? weightSatisfied : (pressers.Count > 0);
            Vector3 target = isHeldDown ? downPos : initialCapLocalPos;
            AnimateTo(target);
        }
        else if (animMode == ButtonAnimMode.StayWhileActive)
        {
            Vector3 target = IsActive ? downPos : initialCapLocalPos;
            AnimateTo(target);
        }
        else if (animMode == ButtonAnimMode.Bounce)
        {
            if (pressers.Count > 0)
            {
                AnimateTo(downPos);
            }
            else
            {
                AnimateBounce();
            }
        }
    }

    private void AnimateBounce()
    {
        if (buttonCap == null || !isActiveAndEnabled) return; // ปิดอยู่ (เช่นตอนหยุดเล่น) สตาร์ท Coroutine ไม่ได้
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

        // รอจนกว่าจะไม่มีใครเหยียบทับอยู่ ถึงจะเด้งกลับขึ้นมา
        while (pressers.Count > 0)
        {
            yield return null;
        }

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
        if (buttonCap == null || !isActiveAndEnabled) return; // ปิดอยู่ (เช่นตอนหยุดเล่น) สตาร์ท Coroutine ไม่ได้
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
        if (satisfied && !weightSatisfied)
        {
            if (IsBlocked) satisfied = false; // ยังไม่นับว่ากดสำเร็จ รอบหน้าที่ตรวจใหม่จะลองอีกครั้ง
            else ToggleSwitch();
        }
        weightSatisfied = satisfied;

        UpdateCapPosition();
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
        if (triggeredByBoxes && !blockedByBox && c.GetComponentInParent<PushableBox>() != null) return true;
        if (triggeredByHairballs && c.GetComponentInParent<Hairball>() != null) return true;

        return false;
    }

    private bool HasBoxOnTop()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return false;

        Bounds b = col.bounds;
        Collider2D[] hits = Physics2D.OverlapBoxAll(b.center, b.size + Vector3.one * (blockCheckMargin * 2f), 0f);
        foreach (var h in hits)
        {
            var box = h.GetComponentInParent<PushableBox>();
            if (box != null && !box.IsBroken && box.gameObject.activeInHierarchy) return true;
        }
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