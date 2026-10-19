using System.Collections;
using UnityEngine;

/// <summary>
/// สัตว์ทดลองตัวที่ 1: ตัวอ้วกก้อนขน (ใช้ยาอาเจียน) = แมว
/// ความสามารถ: อ้วกก้อนขนออกไปตามทิศ ก้อนขนไปตกที่ปลายระยะ (หรือชนกำแพง) แล้วนอนอยู่สักพัก
/// ก้อนขนที่นอนอยู่กดสวิตช์ได้ (เป็นตุ้มถ่วง) เหมาะกับสวิตช์ที่ตัวเองเดินไปไม่ถึง/ต้องมีของวางทับค้างไว้
/// โหมดอัตโนมัติ: ได้ยาแล้วอ้วกไปทิศที่ตั้งไว้ใน Inspector เอง
/// ยาผสม:
///   Skyward (เขียว+ฟ้า) = อ้วกก้อนขนลอยขึ้นข้างบน
///   Havoc   (เขียว+ส้ม) = กระหน่ำกระสุนอ้วก (ก้อนขนหลายก้อนแตกกระจาย)
///   Surge   (ฟ้า+ส้ม)   = อ้วกเป็นก้อนใหญ่ยักษ์
/// </summary>
public class HairballAnimal : TestAnimal
{
    private enum VomitMode { Normal, Skyward, Barrage, Giant }

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

    #region Combo Settings
    [Header("Skyward (เขียว+ฟ้า): อ้วกลอยขึ้นข้างบน")]
    [Tooltip("ระยะที่ก้อนขนลอยขึ้นไปข้างบน")]
    [SerializeField] private float skywardRange = 4f;

    [Tooltip("ความเร็วก้อนขนตอนลอยขึ้น (ช้ากว่าปกติ ดูเหมือนลอย)")]
    [SerializeField] private float skywardSpeed = 4f;

    [Header("Mass (มวลก้อนขน ใช้กับสวิตช์ที่ต้องใช้น้ำหนัก)")]
    [SerializeField] private float normalMass = 1f;
    [SerializeField] private float barrageMass = 0.5f;
    [SerializeField] private float giantMass = 3f;

    [Header("Havoc (เขียว+ส้ม): กระหน่ำกระสุนอ้วก")]
    [Tooltip("จำนวนก้อนขนที่กระหน่ำออกไป")]
    [SerializeField] private int barrageCount = 5;

    [Tooltip("มุมกระจายรวมของกระสุน (องศา)")]
    [SerializeField] private float barrageSpreadAngle = 60f;

    [Tooltip("ระยะบินของกระสุนแต่ละก้อน (ใกล้กว่าปกติ)")]
    [SerializeField] private float barrageRange = 3.5f;

    [Tooltip("ก้อนขนกระสุนนอนอยู่กี่วินาที")]
    [SerializeField] private float barrageLingerSeconds = 3f;

    [Header("Surge (ฟ้า+ส้ม): อ้วกเป็นก้อนใหญ่ยักษ์")]
    [Tooltip("ขยายขนาดก้อนขนกี่เท่า")]
    [SerializeField] private float giantScale = 2.5f;

    [Tooltip("ความเร็วก้อนใหญ่ (หนักกว่า เลยช้ากว่า)")]
    [SerializeField] private float giantSpeed = 5f;

    [Tooltip("ก้อนใหญ่นอนอยู่กี่วินาที")]
    [SerializeField] private float giantLingerSeconds = 12f;

    [Tooltip("ติ๊กไว้ = อ้วกยักษ์สามารถพังกล่องและประตูที่พังได้")]
    [SerializeField] private bool giantBreaksObstacles = true;

    [Tooltip("ความสูงชดเชยเมื่ออ้วกก้อนยักษ์ (กันก้อนใหญ่จมพื้น)")]
    [SerializeField] private float giantSpawnHeightOffset = 0.65f;

    [Tooltip("ระยะห่างจากตัวเพิ่มเติมเมื่ออ้วกก้อนยักษ์")]
    [SerializeField] private float giantSpawnForwardOffset = 0.5f;

    [Header("Gravity (แรงโน้มถ่วง)")]
    [Tooltip("แรงโน้มถ่วงของก้อนขนปกติ/กระสุน/ก้อนยักษ์ (1 = ปกติ)")]
    [SerializeField] private float defaultGravityScale = 1f;

    [Tooltip("แรงโน้มถ่วงของก้อนขน Skyward (0 = ลอยขึ้นข้างบนตามความสามารถ)")]
    [SerializeField] private float skywardGravityScale = 0f;
    #endregion

    #region Auto
    [Header("Auto (โหมดอัตโนมัติ)")]
    [Tooltip("ทิศที่จะอ้วกก้อนขนไปเมื่อได้ยา")]
    [SerializeField] private AutoDirection vomitDirection = AutoDirection.Right;

    protected override IEnumerator AutoRoutine()
    {
        // Skyward จะอ้วกขึ้นข้างบนเสมอ ไม่สนทิศใน Inspector
        Vector2 dir = (mode == VomitMode.Skyward) ? Vector2.up : ToVector(vomitDirection);
        ExecuteAbility(rb.position + dir * 5f);
        yield break;
    }
    #endregion

    #region Potion
    private VomitMode mode = VomitMode.Normal;

    protected override PotionType RequiredPotion => PotionType.Vomit;

    public override bool AcceptsPotion(PotionType potion)
    {
        return potion == PotionType.Vomit || IsComboPotion(potion);
    }

    protected override void OnPotionDrunk(PotionType potion)
    {
        switch (potion)
        {
            case PotionType.Skyward: mode = VomitMode.Skyward; break;
            case PotionType.Havoc: mode = VomitMode.Barrage; break;
            case PotionType.Surge: mode = VomitMode.Giant; break;
            default: mode = VomitMode.Normal; break;
        }
        Debug.Log($"[{animalName}] โหมดอ้วก: {mode}");
    }
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
        if (mode == VomitMode.Skyward) dir = Vector2.up;
        FacingDirection = dir;

        if (stopMovingWhenVomiting) StopMoving();

        switch (mode)
        {
            case VomitMode.Skyward:
                SpawnHairball(dir, skywardSpeed, skywardRange, hairballLingerSeconds, 1f, normalMass, false, skywardGravityScale);
                break;

            case VomitMode.Barrage:
                int count = Mathf.Max(1, barrageCount);
                for (int i = 0; i < count; i++)
                {
                    float t = (count == 1) ? 0.5f : (float)i / (count - 1);
                    float angle = Mathf.Lerp(-barrageSpreadAngle * 0.5f, barrageSpreadAngle * 0.5f, t);
                    Vector2 d = Quaternion.Euler(0f, 0f, angle) * dir;
                    SpawnHairball(d, hairballSpeed, barrageRange, barrageLingerSeconds, 1f, barrageMass, false, defaultGravityScale);
                }
                break;

            case VomitMode.Giant:
                SpawnHairball(dir, giantSpeed, hairballRange, giantLingerSeconds, giantScale, giantMass, giantBreaksObstacles, defaultGravityScale);
                break;

            default:
                SpawnHairball(dir, hairballSpeed, hairballRange, hairballLingerSeconds, 1f, normalMass, false, defaultGravityScale);
                break;
        }
        return true;
    }

    private void SpawnHairball(Vector2 dir, float speed, float range, float linger, float scale, float ballMass, bool canBreak = false, float gravScale = 1f)
    {
        float forwardDist = mouthDistance;
        float heightOffset = 0f;

        if (scale > 1f)
        {
            // ชดเชยความสูงและระยะข้างหน้าสำหรับก้อนขนยักษ์ เพื่อไม่ให้เกิดมาแล้วจมพื้นหรือทับตัวแมว
            heightOffset = giantSpawnHeightOffset;
            forwardDist += giantSpawnForwardOffset;
        }

        Vector2 spawnOffset = dir * forwardDist + Vector2.up * heightOffset;
        float offsetDist = spawnOffset.magnitude;
        if (offsetDist > 0.001f)
        {
            Vector2 offsetDir = spawnOffset / offsetDist;
            RaycastHit2D hit = Physics2D.Raycast(rb.position, offsetDir, offsetDist + 0.1f);
            if (hit.collider != null && !hit.collider.isTrigger && hit.collider.GetComponentInParent<TestAnimal>() == null)
            {
                spawnOffset = offsetDir * Mathf.Max(0.05f, hit.distance - 0.15f);
            }
        }

        Vector2 spawnPos = rb.position + spawnOffset;
        Hairball ball = Instantiate(hairballPrefab, spawnPos, Quaternion.identity);
        if (!Mathf.Approximately(scale, 1f)) ball.transform.localScale *= scale;
        ball.Mass = ballMass;
        ball.CanBreakObstacles = canBreak;
        ball.Init(dir, speed, range, linger, gravScale);
    }
    #endregion
}