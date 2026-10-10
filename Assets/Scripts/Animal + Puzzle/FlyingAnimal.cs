using System.Collections;
using UnityEngine;

/// <summary>
/// สัตว์ทดลองตัวที่ 2: ตัวอ้วนบิน (ใช้ยาตัวเบา)
/// ความสามารถ: ลอยขึ้นกลางอากาศชั่วคราว ลอยข้ามสิ่งกีดขวาง/หลุม (Layer ที่ตั้งไว้) ได้ และเคลื่อนที่เร็วขึ้น
/// ตอนลอยตัวเบาเกินจะไม่กดสวิตช์ (สวิตช์ตั้งให้ข้ามตัวที่ลอยอยู่ได้)
/// โหมดอัตโนมัติ: ได้ยาแล้วลอยขึ้น บินไปทิศที่ตั้งตามระยะที่กำหนด แล้วลงพื้นเอง
/// </summary>
public class FlyingAnimal : TestAnimal
{
    #region Flying Settings
    [Header("Flying (บิน)")]
    [Tooltip("ลอยอยู่นานกี่วินาทีก่อนตกลงพื้นเอง (โหมดคุมเอง)")]
    [SerializeField] private float flyDuration = 4f;

    [Tooltip("ตัวคูณความเร็วเดินตอนลอย")]
    [SerializeField] private float flySpeedMultiplier = 1.3f;

    [Tooltip("Layer ที่ลอยข้ามได้ตอนบิน (เช่น หลุม น้ำ กำแพงเตี้ย) เลือกเฉพาะ Layer ที่อยากให้บินข้าม")]
    [SerializeField] private LayerMask flyOverLayers;
    #endregion

    #region Auto
    [Header("Auto Flight (โหมดอัตโนมัติ)")]
    [Tooltip("ทิศที่จะบินไปเมื่อได้ยา")]
    [SerializeField] private AutoDirection flyDirection = AutoDirection.Right;

    [Tooltip("ระยะที่บินไป (หน่วย Unity) ถึงแล้วจะลงพื้นเอง")]
    [SerializeField] private float flyDistance = 5f;

    protected override IEnumerator AutoRoutine()
    {
        // ขึ้นบิน
        if (!ExecuteAbility(rb.position)) yield break;
        flyEndTime = float.MaxValue; // ไม่ให้ตกกลางทางเพราะหมดเวลา เดี๋ยวลงเองตอนถึงที่หมาย

        // บินไปทิศที่ตั้งไว้
        Vector2 dir = ToVector(flyDirection);
        MoveTo(rb.position + dir * flyDistance);
        while (IsMoving) yield return null;

        // ถึงแล้ว (หรือชนสิ่งกีดขวาง) ลงพื้น
        if (isFlying) Land();
    }
    #endregion

    #region Visual
    [Header("Visual")]
    [Tooltip("สไปรต์ลอยสูงขึ้นกี่หน่วยตอนบิน (ภาพเท่านั้น ไม่กระทบตำแหน่งจริง)")]
    [SerializeField] private float liftHeight = 0.5f;

    [Tooltip("ความเร็วที่สไปรต์ลอยขึ้น/ลงมา")]
    [SerializeField] private float liftSmoothSpeed = 8f;

    [Tooltip("(ไม่บังคับ) เงาใต้ตัว จะโชว์เฉพาะตอนลอย")]
    [SerializeField] private GameObject shadow;
    #endregion

    #region State
    private bool isFlying = false;
    private float flyEndTime;
    private Collider2D bodyCollider;
    private Transform spriteTransform;
    private Vector3 spriteBaseLocalPos;

    public override bool IsAirborne => isFlying;
    protected override float SpeedMultiplier => isFlying ? flySpeedMultiplier : 1f;
    protected override PotionType RequiredPotion => PotionType.Light;
    #endregion

    #region Unity Lifecycle
    protected override void Awake()
    {
        base.Awake();
        bodyCollider = GetComponent<Collider2D>();

        if (spriteRenderer != null)
        {
            if (spriteRenderer.transform == transform)
            {
                // สไปรต์อยู่บนตัวหลัก ถ้ายกสไปรต์จะยกตัวจริงไปด้วยและดึงตำแหน่งกลับ ทำให้บินไม่ไป
                Debug.LogWarning($"[{animalName}] SpriteRenderer อยู่บนตัวหลัก จึงปิดการยกสไปรต์ไว้ " +
                                 "ให้สร้างลูก (child) แล้วย้าย SpriteRenderer ไปไว้ที่ลูกถึงจะเห็นตัวลอยขึ้น");
            }
            else
            {
                spriteTransform = spriteRenderer.transform;
                spriteBaseLocalPos = spriteTransform.localPosition;
            }
        }
        if (shadow != null) shadow.SetActive(false);
    }

    protected override void Update()
    {
        base.Update();

        if (isFlying && Time.time >= flyEndTime) Land();

        // ยกสไปรต์ขึ้น/ลงแบบนุ่มๆ
        if (spriteTransform != null)
        {
            Vector3 target = spriteBaseLocalPos + (isFlying ? Vector3.up * liftHeight : Vector3.zero);
            spriteTransform.localPosition = Vector3.Lerp(spriteTransform.localPosition, target, liftSmoothSpeed * Time.deltaTime);
        }
    }
    #endregion

    #region Ability
    protected override bool PerformAbility(Vector2 aimWorldPoint)
    {
        if (isFlying) Land();
        else TakeOff();
        return true;
    }

    private void TakeOff()
    {
        isFlying = true;
        flyEndTime = Time.time + flyDuration;
        bodyCollider.excludeLayers = flyOverLayers; // ไม่ชน Layer เหล่านี้ตอนลอย
        if (shadow != null) shadow.SetActive(true);
    }

    private void Land()
    {
        isFlying = false;
        bodyCollider.excludeLayers = (LayerMask)0;
        if (shadow != null) shadow.SetActive(false);
    }
    #endregion
}