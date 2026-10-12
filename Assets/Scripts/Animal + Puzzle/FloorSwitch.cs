using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สวิตช์แบบกดรอบเดียว (Toggle): เมื่อมีของที่ถูกต้องเข้ามาแตะ (TriggerEnter) จะสลับสถานะทันที
/// ไม่ต้องรอให้เดินออก และไม่ต้องค้างยืนทับ
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

    #region Visual
    [Header("Visual (Optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite offSprite;
    [SerializeField] private Sprite onSprite;
    #endregion

    #region State
    public bool IsActive { get; private set; }
    public event Action<FloorSwitch> StateChanged;
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
        if (IsValidPresser(other))
        {
            ToggleSwitch();
        }
    }
    #endregion

    #region Logic
    private void ToggleSwitch()
    {
        // สลับสถานะเปิด/ปิด (On <-> Off) ทุกครั้งที่ถูกชน
        IsActive = !IsActive;
        ApplyVisual();
        StateChanged?.Invoke(this);
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