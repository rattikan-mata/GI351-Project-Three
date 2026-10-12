using System.Collections;
using UnityEngine;

/// <summary>
/// กล่องที่ถูกดันได้ (โดย PusherAnimal เท่านั้น สัตว์ตัวอื่นเดินชนไม่ขยับ)
/// เลื่อนเป็นเส้นตรง หยุดเองเมื่อชน Layer ที่ตั้งไว้ใน Obstacle Layer
/// มีแรงโน้มถ่วงจำลอง (ยังเป็น Kinematic แมวเดินชนแล้วกล่องไม่เลื่อนเอง)
/// ต้องมี Rigidbody2D (Kinematic) + Collider2D (แบบไม่ Trigger เพื่อให้สัตว์ชนติด)
/// กล่องทับ FloorSwitch ได้ (สวิตช์ตั้งให้รับกล่องไว้)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PushableBox : MonoBehaviour
{
    #region Settings
    [Header("Collision")]
    [Tooltip("Layer ที่ขวางกล่อง (กำแพง ประตู พื้น กล่องอื่น ฯลฯ) ห้ามรวม Layer ของสวิตช์ เพราะกล่องต้องทับสวิตช์ได้")]
    [SerializeField] private LayerMask obstacleLayer;

    [Tooltip("ช่องว่างเล็กๆ ที่เว้นไว้ระหว่างกล่องกับสิ่งที่ชน กันกล่องติด")]
    [SerializeField] private float skinWidth = 0.02f;

    [Tooltip("ถ้าเลื่อนได้น้อยกว่านี้ถือว่าดันไม่ไป (ติดกำแพงอยู่แล้ว)")]
    [SerializeField] private float minPushDistance = 0.05f;
    #endregion

    #region Gravity
    [Header("Gravity (แรงโน้มถ่วง)")]
    [Tooltip("ติ๊กไว้ = กล่องตกตามแรงโน้มถ่วง (ใช้ค่า Physics 2D Gravity ของโปรเจกต์)")]
    [SerializeField] private bool useGravity = true;

    [Tooltip("ตัวคูณแรงโน้มถ่วง (1 = เท่าค่าใน Project Settings)")]
    [SerializeField] private float gravityScale = 1f;

    [Tooltip("ความเร็วตกสูงสุด กันกล่องทะลุพื้น")]
    [SerializeField] private float maxFallSpeed = 20f;
    #endregion

    #region State
    private Rigidbody2D rb;
    private Collider2D col;
    private bool isMoving = false;
    private float verticalSpeed = 0f;
    private int gravityHoldCount = 0;
    private readonly RaycastHit2D[] castResults = new RaycastHit2D[8];
    public bool IsMoving => isMoving;
    public bool IsGravityHeld => gravityHoldCount > 0;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f; // จำลองเอง เพื่อไม่ให้แมวเดินชนแล้วกล่องเลื่อน
        col = GetComponent<Collider2D>();
    }

    private void FixedUpdate()
    {
        if (isMoving) return; // ตอนถูกดัน SlideRoutine รวมแรงโน้มถ่วงให้แล้ว
        ApplyMove(GravityDelta());
    }
    #endregion

    #region Gravity API
    /// <summary>สายพานเรียกเพื่อพักแรงโน้มถ่วง กันกล่องตกทะลุสายพานที่เป็น Trigger</summary>
    public void AddGravityHold()
    {
        gravityHoldCount++;
        verticalSpeed = 0f;
    }

    public void RemoveGravityHold()
    {
        gravityHoldCount = Mathf.Max(0, gravityHoldCount - 1);
    }
    #endregion

    #region Push API
    /// <summary>ดันกล่องไปทิศ direction ไกลสุด distance คืนค่า true ถ้าดันไปได้จริง</summary>
    public bool TryPush(Vector2 direction, float distance, float speed)
    {
        if (isMoving) return false;

        direction.Normalize();
        float freeDistance = GetFreeDistance(direction, distance);
        if (freeDistance < minPushDistance) return false;

        StartCoroutine(SlideRoutine(direction, freeDistance, speed));
        return true;
    }

    /// <summary>วัดว่าเลื่อนไปทางนั้นได้ไกลสุดเท่าไร ก่อนจะชนสิ่งกีดขวาง</summary>
    private float GetFreeDistance(Vector2 direction, float maxDistance)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleLayer);
        filter.useTriggers = false;

        int count = col.Cast(direction, filter, castResults, maxDistance);
        float free = maxDistance;
        for (int i = 0; i < count; i++)
        {
            free = Mathf.Min(free, castResults[i].distance - skinWidth);
        }
        return Mathf.Max(free, 0f);
    }

    private IEnumerator SlideRoutine(Vector2 direction, float distance, float speed)
    {
        isMoving = true;
        float moved = 0f;

        while (moved < distance)
        {
            float step = Mathf.Min(speed * Time.fixedDeltaTime, distance - moved);
            ApplyMove(direction * step + GravityDelta());
            moved += step;
            yield return new WaitForFixedUpdate();
        }

        isMoving = false;
    }

    private Vector2 GravityDelta()
    {
        if (!useGravity || gravityHoldCount > 0 || gravityScale <= 0f)
        {
            verticalSpeed = 0f;
            return Vector2.zero;
        }

        verticalSpeed += Physics2D.gravity.y * gravityScale * Time.fixedDeltaTime;
        verticalSpeed = Mathf.Max(verticalSpeed, -maxFallSpeed);

        if (Mathf.Abs(verticalSpeed) < 0.0001f) return Vector2.zero;

        bool falling = verticalSpeed < 0f;
        Vector2 dir = falling ? Vector2.down : Vector2.up;
        float desired = Mathf.Abs(verticalSpeed) * Time.fixedDeltaTime;
        float free = GetFreeDistance(dir, desired);
        if (free < desired - 0.0001f) verticalSpeed = 0f;
        return dir * free;
    }

    private void ApplyMove(Vector2 delta)
    {
        if (delta.sqrMagnitude < 0.0000001f) return;
        rb.MovePosition(rb.position + delta);
    }
    #endregion
}
