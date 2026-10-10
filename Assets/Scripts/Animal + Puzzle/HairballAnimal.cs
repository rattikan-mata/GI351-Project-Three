using System.Collections;
using UnityEngine;

/// <summary>
/// สัตว์ทดลองตัวที่ 1: ตัวอ้วกก้อนขน (ใช้ยาอาเจียน)
/// ความสามารถ: อ้วกก้อนขนออกไปตามทิศ ก้อนขนไปตกที่ปลายระยะ (หรือชนกำแพง) แล้วนอนอยู่สักพัก
/// ก้อนขนที่นอนอยู่กดสวิตช์ได้ (เป็นตุ้มถ่วง) เหมาะกับสวิตช์ที่ตัวเองเดินไปไม่ถึง/ต้องมีของวางทับค้างไว้
/// โหมดอัตโนมัติ: ได้ยาแล้วอ้วกไปทิศที่ตั้งไว้ใน Inspector เอง
/// </summary>
public class HairballAnimal : TestAnimal
{
    #region Vomit Settings
    [Header("Vomit (อ้วกก้อนขน)")]
    [Tooltip("ลาก Prefab ก้อนขนที่มี Hairball.cs มาใส่")]
    [SerializeField] private Hairball hairballPrefab;

    [Tooltip("ระยะที่ก้อนขนเกิดห่างจากตัวสัตว์ (ตรงปาก)")]
    [SerializeField] private float mouthDistance = 0.6f;

    [Tooltip("ความเร็วที่ก้อนขนพุ่งออกไป")]
    [SerializeField] private float hairballSpeed = 8f;

    [Tooltip("ระยะบินสูงสุดของก้อนขน (หน่วย Unity)")]
    [SerializeField] private float hairballRange = 5f;

    [Tooltip("ก้อนขนนอนอยู่กี่วินาทีหลังตกถึงพื้น ก่อนจะหายไป")]
    [SerializeField] private float hairballLingerSeconds = 6f;

    [Tooltip("ติ๊กไว้ = ตอนอ้วกจะหยุดเดินทันที")]
    [SerializeField] private bool stopMovingWhenVomiting = true;
    #endregion

    #region Auto
    [Header("Auto (โหมดอัตโนมัติ)")]
    [Tooltip("ทิศที่จะอ้วกก้อนขนไปเมื่อได้ยา")]
    [SerializeField] private AutoDirection vomitDirection = AutoDirection.Right;

    protected override IEnumerator AutoRoutine()
    {
        Vector2 dir = ToVector(vomitDirection);
        ExecuteAbility(rb.position + dir * 5f);
        yield break;
    }
    #endregion

    #region Potion
    protected override PotionType RequiredPotion => PotionType.Vomit;
    #endregion

    #region Ability
    protected override bool PerformAbility(Vector2 aimWorldPoint)
    {
        if (hairballPrefab == null)
        {
            Debug.LogWarning($"[{animalName}] ยังไม่ได้ลาก Hairball Prefab ใส่ช่อง Hairball Prefab");
            return false;
        }

        Vector2 dir = aimWorldPoint - rb.position;
        if (dir.sqrMagnitude < 0.0001f) dir = FacingDirection;
        dir.Normalize();
        FacingDirection = dir;

        if (stopMovingWhenVomiting) StopMoving();

        Vector2 spawnPos = rb.position + dir * mouthDistance;
        Hairball ball = Instantiate(hairballPrefab, spawnPos, Quaternion.identity);
        ball.Init(dir, hairballSpeed, hairballRange, hairballLingerSeconds);
        return true;
    }
    #endregion
}