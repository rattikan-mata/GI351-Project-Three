using UnityEngine;

/// <summary>
/// ประตูแบบสลับสถานะ (Toggle): เมื่อสวิตช์ถูกกด (ส่งสัญญาณ StateChanged มา)
/// จะสลับสถานะเปิด/ปิดของประตูสลับกันไปเรื่อยๆ และค้างสถานะนั้นไว้ทันที
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
        IsOpen = startsOpen;
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