using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ช่องใส่ยา 1 ช่อง (ใช้คู่กับ PotionSequenceDropper)
/// ใส่ยาได้ด้วยการเรียก TryInsert() (ผ่าน PotionSequenceDropper.TryAddPotion ของระบบปรุงยา)
/// มีโหมดทดสอบ: ตั้ง Test Click Potion แล้วคลิกที่ช่องจะใส่ยาชนิดนั้นเลย (ไม่ต้องรอระบบของเพื่อน)
/// ต้องมี Collider2D (เฉพาะถ้าจะใช้โหมดคลิกทดสอบ)
/// </summary>
public class PotionSlot : MonoBehaviour
{
    #region Settings
    [Header("Visual (Optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite emptySprite;
    [SerializeField] private Sprite filledSprite;

    [Header("Test Mode (ทดสอบ)")]
    [Tooltip("ตั้งเป็นชนิดยา = คลิกที่ช่องแล้วใส่ยาชนิดนั้นทันที (ใช้ทดสอบ) / None = ปิดโหมดนี้")]
    [SerializeField] private PotionType testClickPotion = PotionType.None;
    #endregion

    #region State
    public bool IsOccupied { get; private set; }
    public PotionType StoredPotion { get; private set; } = PotionType.None;

    /// <summary>ถูกเรียกตอนใส่ยาสำเร็จ (PotionSequenceDropper ฟังเหตุการณ์นี้)</summary>
    public event Action<PotionSlot, PotionType> PotionInserted;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        ApplyVisual();
    }

    private void OnMouseDown()
    {
        if (testClickPotion == PotionType.None) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        TryInsert(testClickPotion);
    }
    #endregion

    #region API
    public bool TryInsert(PotionType potion)
    {
        if (IsOccupied || potion == PotionType.None) return false;

        IsOccupied = true;
        StoredPotion = potion;
        ApplyVisual();
        PotionInserted?.Invoke(this, potion);
        return true;
    }

    /// <summary>เคลียร์ช่อง (Dropper เรียกตอนปล่อยยาออกไป)</summary>
    public void Clear()
    {
        IsOccupied = false;
        StoredPotion = PotionType.None;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (spriteRenderer == null) return;
        Sprite s = IsOccupied ? filledSprite : emptySprite;
        if (s != null) spriteRenderer.sprite = s;
    }
    #endregion
}
