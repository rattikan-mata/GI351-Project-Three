using System.Collections;
using UnityEngine;

/// <summary>
/// สัตว์ทดลองตัวที่ 3: ตัวผลัก/ดันกล่อง (ใช้ยาเพิ่มพละกำลัง)
/// ความสามารถ: ดันกล่อง (PushableBox) ที่อยู่ใกล้และอยู่ทางเดียวกับทิศที่เล็ง ให้เลื่อนไปตามทิศนั้น
/// กล่องจะหยุดเองเมื่อชนกำแพง/ประตู/กล่องอื่น ใช้ดันกล่องไปทับสวิตช์ได้
/// โหมดอัตโนมัติ: ได้ยาแล้วหากล่องที่ใกล้ที่สุด เดินไปยืนด้านหลัง แล้วผลักไปทิศที่ตั้งไว้เอง
/// </summary>
public class PusherAnimal : TestAnimal
{
    #region Push Settings
    [Header("Push (ดันกล่อง)")]
    [Tooltip("Layer ของกล่องที่ดันได้")]
    [SerializeField] private LayerMask boxLayer;

    [Tooltip("ระยะที่เอื้อมถึงกล่อง (นับจากกลางตัว)")]
    [SerializeField] private float pushReach = 1.3f;

    [Tooltip("ระยะที่ดันกล่องให้เลื่อนไปต่อ 1 ครั้ง")]
    [SerializeField] private float pushDistance = 1.5f;

    [Tooltip("ความเร็วที่กล่องเลื่อน")]
    [SerializeField] private float pushSpeed = 6f;

    [Tooltip("ติ๊กไว้ = ดันได้แค่ 4 ทิศตรง (บน/ล่าง/ซ้าย/ขวา) เหมาะกับปริศนากล่อง / ไม่ติ๊ก = ดันตามทิศเป๊ะๆ")]
    [SerializeField] private bool snapToCardinalDirections = true;

    [Tooltip("ทิศกล่องต้องตรงกับทิศที่เล็งแค่ไหนถึงจะดัน (1 = ตรงเป๊ะ, 0 = ขอแค่อยู่ครึ่งวงกลมข้างหน้า)")]
    [SerializeField, Range(0f, 1f)] private float aimAlignment = 0.5f;
    #endregion

    #region Auto
    [Header("Auto Push (โหมดอัตโนมัติ)")]
    [Tooltip("ทิศที่จะผลักกล่องไปเมื่อได้ยา")]
    [SerializeField] private AutoDirection pushDirection = AutoDirection.Right;

    [Tooltip("รัศมีที่ใช้หากล่องที่ใกล้ที่สุด (นับจากตัวสัตว์)")]
    [SerializeField] private float autoSearchRadius = 6f;

    [Tooltip("จำนวนครั้งที่ผลักต่อกัน (สัตว์จะเดินตามกล่องไปผลักต่อ)")]
    [SerializeField] private int autoPushCount = 1;

    [Tooltip("ระยะที่ไปยืนห่างจากกลางกล่องด้านหลัง (ควรน้อยกว่า Push Reach)")]
    [SerializeField] private float standOffset = 1f;

    protected override IEnumerator AutoRoutine()
    {
        Vector2 dir = ToVector(pushDirection);
        if (snapToCardinalDirections)
        {
            dir = (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                ? new Vector2(Mathf.Sign(dir.x), 0f)
                : new Vector2(0f, Mathf.Sign(dir.y));
        }

        PushableBox box = FindNearestBox();
        if (box == null)
        {
            Debug.LogWarning($"[{animalName}] ไม่เจอกล่องในรัศมี {autoSearchRadius}");
            yield break;
        }

        for (int i = 0; i < autoPushCount; i++)
        {
            if (i > 0) yield return new WaitForSeconds(abilityCooldown);

            // เดินไปยืนด้านหลังกล่อง (ฝั่งตรงข้ามทิศที่จะผลัก)
            Vector2 standPos = (Vector2)box.transform.position - dir * standOffset;
            MoveTo(standPos);
            while (IsMoving) yield return null;

            // ผลัก
            if (!ExecuteAbility(rb.position + dir)) yield break; // ผลักไม่ไปแล้ว (ติดกำแพง) เลิก

            while (box.IsMoving) yield return null;
        }
    }

    private PushableBox FindNearestBox()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, autoSearchRadius, boxLayer);
        PushableBox best = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<PushableBox>(out var b)) continue;
            float d = Vector2.Distance(rb.position, b.transform.position);
            if (d < bestDist) { bestDist = d; best = b; }
        }
        return best;
    }
    #endregion

    #region Potion
    protected override PotionType RequiredPotion => PotionType.Strength;
    #endregion

    #region Ability
    protected override bool PerformAbility(Vector2 aimWorldPoint)
    {
        Vector2 dir = aimWorldPoint - rb.position;
        if (dir.sqrMagnitude < 0.0001f) dir = FacingDirection;

        if (snapToCardinalDirections)
        {
            dir = (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                ? new Vector2(Mathf.Sign(dir.x), 0f)
                : new Vector2(0f, Mathf.Sign(dir.y));
        }
        dir.Normalize();
        FacingDirection = dir;

        PushableBox box = FindBoxInFront(dir);
        if (box == null) return false; // ไม่มีกล่องให้ดัน ไม่เริ่ม cooldown

        StopMoving();
        return box.TryPush(dir, pushDistance, pushSpeed);
    }

    private PushableBox FindBoxInFront(Vector2 dir)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, pushReach, boxLayer);
        PushableBox best = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<PushableBox>(out var box)) continue;

            Vector2 toBox = (Vector2)box.transform.position - rb.position;
            if (Vector2.Dot(toBox.normalized, dir) < aimAlignment) continue;

            float dist = toBox.magnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = box;
            }
        }
        return best;
    }
    #endregion

    #region Debug Gizmos
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, pushReach);
    }
    #endregion
}