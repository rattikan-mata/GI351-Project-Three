using UnityEngine;

/// <summary>
/// ประตู: เปิดเมื่อสวิตช์ที่ผูกไว้ทำงาน (ครบทุกตัว หรือแค่ตัวใดตัวหนึ่งก็ได้)
/// ปิดอยู่ = Collider ขวางทาง / เปิดอยู่ = ปิด Collider ให้เดินผ่านได้
/// ลาก FloorSwitch ใส่ช่อง Switches ได้หลายตัว
/// </summary>
public class Door : MonoBehaviour
{
    #region Switch Link
    [Header("Switch Link")]
    [SerializeField] private FloorSwitch[] switches;

    [Tooltip("ติ๊กไว้ = ต้องกดสวิตช์ครบทุกตัวพร้อมกัน / ไม่ติ๊ก = ตัวใดตัวหนึ่งก็เปิดได้")]
    [SerializeField] private bool requireAll = true;

    [Tooltip("ติ๊กไว้ = เปิดแล้วค้างเปิดตลอด แม้สวิตช์จะปล่อยแล้ว")]
    [SerializeField] private bool stayOpenOnceOpened = false;
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
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (blockingCollider == null) blockingCollider = GetComponent<Collider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
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

    private void Start()
    {
        Evaluate();
        ApplyState(); // ตั้งสถานะเริ่มต้นให้ตรงกับภาพ/Collider
    }
    #endregion

    #region Logic
    private void OnSwitchChanged(FloorSwitch changed) => Evaluate();

    private void Evaluate()
    {
        if (IsOpen && stayOpenOnceOpened) return;

        bool shouldOpen = ShouldOpen();
        if (shouldOpen == IsOpen) return;

        IsOpen = shouldOpen;
        ApplyState();
    }

    private bool ShouldOpen()
    {
        if (switches == null || switches.Length == 0) return false;

        bool any = false;
        bool all = true;
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
        if (blockingCollider != null) blockingCollider.enabled = !IsOpen;

        if (spriteRenderer != null)
        {
            Sprite s = IsOpen ? openSprite : closedSprite;
            if (s != null) spriteRenderer.sprite = s;
        }

        if (animator != null && !string.IsNullOrEmpty(openBoolParameter))
        {
            animator.SetBool(openBoolParameter, IsOpen);
        }
    }
    #endregion
}
