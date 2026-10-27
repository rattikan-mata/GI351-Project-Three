using UnityEngine;

/// <summary>
/// แอนิเมชันสัตว์แบบสลับภาพ (ไม่ต้องใช้ Animator)
/// - ยืนเฉยๆ: วนภาพ Idle
/// - กำลังเดิน/บิน (มีเป้าหมายหรือมีความเร็ว): วนภาพ Move (ถ้าว่างไว้ใช้ Idle)
/// - ทำท่า (เช่น แมวอ้วก/เป่าฟอง): เรียก PlayAction(วินาที) จะโชว์ภาพ Action ตามเวลาที่กำหนด
/// คุมรูปของ SpriteRenderer ทุกเฟรม ทิศซ้าย/ขวา (flipX) ยังเป็นของสคริปต์สัตว์เหมือนเดิม
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AnimalSpriteAnimator : MonoBehaviour
{
    [SerializeField] private TestAnimal animal;

    [Header("Idle (ยืนเฉยๆ)")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField, Min(0.1f)] private float idleFps = 3f;

    [Header("Move (เดิน/บิน)")]
    [SerializeField] private Sprite[] moveFrames;
    [SerializeField, Min(0.1f)] private float moveFps = 6f;
    [Tooltip("ความเร็วขั้นต่ำที่นับว่ากำลังเคลื่อนที่")]
    [SerializeField] private float moveSpeedThreshold = 0.15f;

    [Header("Action (ทำท่า)")]
    [SerializeField] private Sprite[] actionFrames;
    [SerializeField, Min(0.1f)] private float actionFps = 6f;

    [Tooltip("ปิดไว้ = ไม่ใช้ภาพ Move แม้กำลังเคลื่อนที่ (สคริปต์สัตว์สลับเองได้ เช่น แพะใช้ท่าเดินเฉพาะบนเพดาน)")]
    [SerializeField] private bool allowMove = true;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Sprite[] current;
    private int frame;
    private float timer;
    private float actionUntil;
    private Sprite[] heldFrames;
    private float heldFps;

    public bool AllowMove { get => allowMove; set => allowMove = value; }

    /// <summary>ค้างภาพชุดนี้ไว้ (ทับทุกท่า) จนกว่าจะเรียก Release</summary>
    public void Hold(Sprite[] frames, float fps = 6f)
    {
        heldFrames = frames;
        heldFps = Mathf.Max(0.1f, fps);
    }

    public void Release()
    {
        heldFrames = null;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        if (animal == null) animal = GetComponent<TestAnimal>();
    }

    /// <summary>โชว์ภาพท่า Action เป็นเวลา seconds วินาที</summary>
    public void PlayAction(float seconds)
    {
        if (actionFrames == null || actionFrames.Length == 0) return;
        actionUntil = Mathf.Max(actionUntil, Time.time + seconds);
    }

    private void LateUpdate()
    {
        Sprite[] set = PickSet(out float fps);
        if (set == null || set.Length == 0) return;

        if (set != current)
        {
            current = set;
            frame = 0;
            timer = 0f;
        }
        else
        {
            timer += Time.deltaTime;
            float step = 1f / fps;
            while (timer >= step)
            {
                timer -= step;
                frame = (frame + 1) % set.Length;
            }
        }

        spriteRenderer.sprite = set[frame];
    }

    private Sprite[] PickSet(out float fps)
    {
        if (heldFrames != null && heldFrames.Length > 0)
        {
            fps = heldFps;
            return heldFrames;
        }

        if (Time.time < actionUntil && actionFrames != null && actionFrames.Length > 0)
        {
            fps = actionFps;
            return actionFrames;
        }

        bool moving = (animal != null && animal.IsMoving) ||
                      (rb != null && rb.linearVelocity.sqrMagnitude > moveSpeedThreshold * moveSpeedThreshold);
        if (moving && allowMove && moveFrames != null && moveFrames.Length > 0)
        {
            fps = moveFps;
            return moveFrames;
        }

        fps = idleFps;
        return idleFrames;
    }
}
