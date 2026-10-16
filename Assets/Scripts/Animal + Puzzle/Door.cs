using System.Collections.Generic;
using PuzzleEngine;
using UnityEngine;

/// <summary>
/// ประตูแบบระดับ (ไม่ใช่การสลับ)
/// เปิดเมื่อเงื่อนไขของสวิตช์เป็นจริง ปิดเมื่อเป็นเท็จ
/// - requireAll ติ๊ก = ทุกสวิตช์ต้อง ON (ค่าเริ่มต้น) / ไม่ติ๊ก = สวิตช์ตัวใดตัวหนึ่ง ON ก็เปิด
/// - ไม่มีสวิตช์ผูกอยู่ = ใช้ Starts Open ตลอด
/// ตรรกะการรวมสัญญาณอยู่ใน PuzzleEngine.Gate ชื่อฟิลด์เดิมคงไว้เพื่อให้ฉากเดิมยังใช้ได้
/// </summary>
public class Door : MonoBehaviour
{
    #region Switch Link
    [Header("Start State (ใช้เฉพาะตอนไม่มีสวิตช์ผูกอยู่)")]
    [Tooltip("ติ๊กไว้ = เริ่มต้นเปิดอยู่ / ไม่ติ๊ก = เริ่มต้นปิดอยู่")]
    [SerializeField] private bool startsOpen = false;

    [Header("Switch Link")]
    [SerializeField] private FloorSwitch[] switches;

    [Tooltip("ติ๊กไว้ = ทุกสวิตช์ต้อง ON ประตูถึงจะเปิด / ไม่ติ๊ก = สวิตช์ตัวใดตัวหนึ่ง ON ก็เปิด")]
    [SerializeField] private bool requireAll = true;
    #endregion

    #region Slide
    [Header("Slide (เลื่อนระหว่างจุดปิด/จุดเปิด)")]
    [Tooltip("(ไม่บังคับ) จุดที่ประตูอยู่ตอนปิด ว่างไว้ = ตำแหน่งของประตูตอนเริ่มเกม\n" +
             "ใช้ GameObject ว่างที่ไม่ได้เป็นลูกของประตู (ถ้าเป็นลูก จะถูกอ่านตำแหน่งตอนเริ่มเกมครั้งเดียว)")]
    [SerializeField] private Transform closedPoint;

    [Tooltip("(ไม่บังคับ) จุดที่ประตูเลื่อนไปอยู่ตอนเปิด ถ้าใส่ไว้จะใช้จุดนี้แทน Open Offset")]
    [SerializeField] private Transform openPoint;

    [Tooltip("ถ้าไม่ได้ใส่ Open Point: ตอนเปิดประตูเลื่อนไปจากจุดปิดตามระยะนี้ (หน่วย Unity, 0,0 = ไม่เลื่อน)")]
    [SerializeField] private Vector2 openOffset = Vector2.zero;

    [Tooltip("ความเร็วที่ประตูเลื่อน (หน่วย Unity ต่อวินาที) 0 = วาร์ปไปทันที")]
    [SerializeField] private float slideSpeed = 4f;

    [Tooltip("ติ๊กไว้ = ปิดการชนของประตูเมื่อเลื่อนถึงจุดเปิดแล้ว (เริ่มปิดเมื่อไหร่จะกลับมาขวางทันที)\n" +
             "ไม่ติ๊ก = การชนเลื่อนตามประตูไปด้วย")]
    [SerializeField] private bool disableColliderWhenOpen = true;
    #endregion

    #region Visual
    [Header("Visual (Optional)")]
    [Tooltip("Collider ที่ขวางทางตอนปิด (ถ้าไม่ใส่จะใช้ Collider2D บนตัวนี้เอง)")]
    [SerializeField] private Collider2D blockingCollider;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;

    [Tooltip("(ไม่บังคับ) Animator ที่มี Bool Parameter สำหรับสถานะเปิด")]
    [SerializeField] private Animator animator;
    [SerializeField] private string openBoolParameter = "Open";
    #endregion

    #region State
    public bool IsOpen { get; private set; }

    private readonly List<ISignal> signals = new List<ISignal>();
    private Vector3 closedPos;
    private Vector3 openPos;
    private bool positionsCached = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (blockingCollider == null) blockingCollider = GetComponent<Collider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        BuildSignals();
        CachePositions();
        IsOpen = ComputeOpen();
        transform.position = TargetPosition; // เริ่มที่จุดของสถานะเริ่มต้นเลย ไม่ต้องเลื่อน
        ApplyState();
    }

    private void OnEnable()
    {
        foreach (ISignal s in signals) s.Changed += Refresh;
    }

    private void OnDisable()
    {
        foreach (ISignal s in signals) s.Changed -= Refresh;
    }

    private void Update()
    {
        Vector3 target = TargetPosition;
        if (transform.position != target)
        {
            transform.position = (slideSpeed > 0f)
                ? Vector3.MoveTowards(transform.position, target, slideSpeed * Time.deltaTime)
                : target;
        }

        // ปิดการชนเมื่อเลื่อนถึงจุดเปิดแล้วเท่านั้น (ตอนกำลังเลื่อนยังขวางอยู่)
        if (blockingCollider != null)
        {
            bool arrivedOpen = IsOpen && transform.position == openPos;
            blockingCollider.enabled = !(disableColliderWhenOpen && arrivedOpen);
        }
    }
    #endregion

    #region Positions
    private Vector3 TargetPosition => IsOpen ? openPos : closedPos;

    /// <summary>อ่านตำแหน่งจุดปิด/จุดเปิดเก็บไว้ครั้งเดียว (กันจุดที่เป็นลูกของประตูเลื่อนตามประตูไปด้วย)</summary>
    private void CachePositions()
    {
        if (positionsCached) return;

        closedPos = (closedPoint != null) ? closedPoint.position : transform.position;
        openPos = (openPoint != null)
            ? openPoint.position
            : closedPos + (Vector3)openOffset;

        // คงค่า z เดิมของประตูไว้ (ไม่ให้เลื่อนลึก/ตื้นตามจุดที่ตั้ง)
        closedPos.z = transform.position.z;
        openPos.z = transform.position.z;

        positionsCached = true;
    }
    #endregion

    #region Logic
    private void BuildSignals()
    {
        signals.Clear();
        if (switches == null) return;

        foreach (FloorSwitch sw in switches)
        {
            if (sw != null) signals.Add(sw);
        }
    }

    /// <summary>ประตูควรเปิดหรือไม่ตามสถานะสวิตช์ตอนนี้</summary>
    private bool ComputeOpen()
    {
        if (signals.Count == 0) return startsOpen;
        return Gate.Evaluate(requireAll ? GateMode.All : GateMode.Any, signals);
    }

    /// <summary>เรียกเมื่อสวิตช์ตัวใดตัวหนึ่งสลับ</summary>
    private void Refresh()
    {
        bool shouldOpen = ComputeOpen();
        if (shouldOpen == IsOpen) return;

        IsOpen = shouldOpen;
        ApplyState();
    }

    private void ApplyState()
    {
        if (spriteRenderer != null)
        {
            Sprite s = IsOpen ? openSprite : closedSprite;
            if (s != null) spriteRenderer.sprite = s;
        }

        if (animator != null && !string.IsNullOrEmpty(openBoolParameter))
        {
            animator.SetBool(openBoolParameter, IsOpen);
        }
        // สถานะการชนอัปเดตใน Update (ปิดการชนเมื่อประตูเลื่อนถึงจุดเปิดแล้ว / กลับมาขวางทันทีที่เริ่มปิด)
    }
    #endregion

    #region Debug Gizmos
    private void OnDrawGizmosSelected()
    {
        Vector3 closed = Application.isPlaying && positionsCached
            ? closedPos
            : (closedPoint != null ? closedPoint.position : transform.position);
        Vector3 open = Application.isPlaying && positionsCached
            ? openPos
            : (openPoint != null ? openPoint.position : closed + (Vector3)openOffset);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(closed, 0.2f);   // จุดปิด
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(open, 0.2f);     // จุดเปิด
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(closed, open);
    }
    #endregion
}
