using UnityEngine;

/// <summary>
/// ประตูแบบสลับสถานะ (Toggle): เมื่อสวิตช์ถูกกด (ส่งสัญญาณ StateChanged มา)
/// จะสลับสถานะเปิด/ปิดของประตูสลับกันไปเรื่อยๆ และค้างสถานะนั้นไว้
/// ประตูเลื่อนไปมาระหว่าง "จุดปิด" กับ "จุดเปิด" ได้ (ตั้งจุดใน Inspector)
/// </summary>
public class Door : MonoBehaviour
{
    #region Switch Link
    [Header("Start State (สถานะตอนเริ่มเกม)")]
    [Tooltip("ติ๊กไว้ = เริ่มต้นเปิดอยู่ / ไม่ติ๊ก = เริ่มต้นปิดอยู่")]
    [SerializeField] private bool startsOpen = false;

    [Header("Switch Link")]
    [SerializeField] private FloorSwitch[] switches;

    [Tooltip("ติ๊กไว้ = ต้องกดสวิตช์ครบทุกตัวพร้อมกันถึงจะสลับ / ไม่ติ๊ก = ตัวใดตัวหนึ่งก็สลับได้")]
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

    private Vector3 closedPos;
    private Vector3 openPos;
    private bool positionsCached = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (blockingCollider == null) blockingCollider = GetComponent<Collider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        CachePositions();
        IsOpen = startsOpen;
        transform.position = TargetPosition; // เริ่มที่จุดของสถานะเริ่มต้นเลย ไม่ต้องเลื่อน
        ApplyState();
    }

    private void OnEnable()
    {
        foreach (var sw in switches)
        {
            if (sw != null) sw.StateChanged += OnSwitchChanged;
        }
    }

    private void OnDisable()
    {
        foreach (var sw in switches)
        {
            if (sw != null) sw.StateChanged -= OnSwitchChanged;
        }
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
    private void OnSwitchChanged(FloorSwitch changed)
    {
        Debug.Log("สวิตช์ส่งสัญญาณมาที่ประตูแล้ว! สถานะสวิตช์คือ: " + changed.IsActive);
        if (changed.IsActive)
        {
            IsOpen = !IsOpen;
            ApplyState();
        }
    }

    private bool CheckAllSwitchesTriggered()
    {
        if (switches == null || switches.Length == 0) return false;

        bool all = true;
        bool any = false;
        foreach (var sw in switches)
        {
            if (sw == null) continue;
            if (sw.IsActive) any = true;
            else all = false;
        }
        return requireAll ? all : any;
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
