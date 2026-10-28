using UnityEngine;

/// <summary>
/// ประตูแบบสลับสถานะ (Toggle): เมื่อสวิตช์ถูกกด (ส่งสัญญาณ StateChanged มา)
/// จะสลับสถานะเปิด/ปิดของประตูสลับกันไปเรื่อยๆ และค้างสถานะนั้นไว้
/// ประตูเลื่อนไปมาระหว่าง "จุดปิด" กับ "จุดเปิด" ได้ (ตั้งจุดใน Inspector)
/// สามารถตั้งค่าให้ "พังได้" (Breakable) เมื่อติ๊กเลือกใน Inspector
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

    #region Support Boxes
    [Header("Support Boxes (กล่องที่ค้ำประตูไว้ไม่ให้ปิดลงมา)")]
    [Tooltip("กล่องที่วางอยู่ใต้ประตู: ตอนประตูปิดลง จะหยุดค้างอยู่บนหัวกล่อง ตราบใดที่กล่องยังไม่พังและยังอยู่ใต้ประตู\n" +
             "พอกล่องพัง (หรือถูกดันออกไป) ประตูจะปิดลงมาจนสุดจุดปิด")]
    [SerializeField] private PushableBox[] supportBoxes;

    [Tooltip("ช่องว่างระหว่างขอบล่างประตูกับหัวกล่องที่ค้ำ (หน่วย Unity) ถ้าเป็น 0 ประตูจะติดกล่องจนฟิสิกส์ถือว่าซ้อนกัน แล้วกล่องผลักไม่ไป\n" +
             "ควรมากกว่า Skin Width ของกล่อง + Contact Offset ของ Physics 2D (ประมาณ 0.03)")]
    [SerializeField, Min(0f)] private float supportGap = 0.08f;
    #endregion

    #region Breakable
    [Header("Breakable Door (ประตูพังได้)")]
    [Tooltip("ติ๊กไว้ = ประตูนี้สามารถพังได้ (ไม่ว่าจะเป็นสวิตช์สั่ง หรือถูกชน)")]
    [SerializeField] private bool isBreakable = false;

    [Tooltip("ติ๊กไว้ = พังเมื่อสวิตช์ส่งสัญญาณเปิด (แทนที่จะค่อยๆ เลื่อนเปิด)")]
    [SerializeField] private bool breakOnSwitch = true;

    [Tooltip("ติ๊กไว้ = พังเมื่อถูกชน (สัตว์ หรือ กล่อง)")]
    [SerializeField] private bool breakOnCollision = true;

    [Tooltip("ติ๊กไว้ = พังเฉพาะเมื่อสัตว์ที่มาชนได้รับยาเสริมพลังแล้ว")]
    [SerializeField] private bool requirePoweredAnimal = false;

    [Tooltip("ติ๊กไว้ = สัตว์ที่มาชนต้องได้รับยาที่มีพลังทำลายสิ่งกีดขวางจริง (เช่น ตัวบินต้องพุ่งชน IsRamming จากยา Surge / แพะต้องเป็นโหมดทุบ) ชนธรรมดาจะไม่พัง")]
    [SerializeField] private bool requireBreakAbility = true;

    [Tooltip("ติ๊กไว้ = ตัวบิน (FlyingAnimal) สามารถพุ่งชนทำลายประตูนี้ได้ (เฉพาะตอนพุ่งชน)")]
    [SerializeField] private bool canBeBrokenByFlyer = true;

    [Tooltip("ติ๊กไว้ = แพะ (PusherAnimal) สามารถชนทำลายประตูนี้ได้ (เฉพาะตอนอยู่ในโหมดทุบ)")]
    [SerializeField] private bool canBeBrokenByPusher = true;

    [Header("Break Visuals (ภาพและเอฟเฟกต์ตอนพัง)")]
    [Tooltip("(ไม่บังคับ) ภาพสไปรต์ซากประตูตอนพังแล้ว ถ้าไม่ใส่จะซ่อนหรือลบประตูทิ้ง")]
    [SerializeField] private Sprite brokenSprite;

    [Tooltip("(ไม่บังคับ) Prefab เอฟเฟกต์ตอนพัง (เช่น เศษไม้แตก ควันระเบิด)")]
    [SerializeField] private GameObject breakEffectPrefab;

    [Tooltip("ติ๊กไว้ = ลบ GameObject ประตูทิ้งทันทีเมื่อพัง / ไม่ติ๊ก = ปิด Collider และคงภาพซากไว้")]
    [SerializeField] private bool destroyOnBreak = false;
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

    #region Audio
    [Header("Audio (เสียง)")]
    [Tooltip("เสียงตอนประตูเปิด")]
    [SerializeField] private AudioClip openSound;

    [Range(0f, 1f)]
    [Tooltip("ระดับความดังเสียงตอนประตูเปิด (0 - 1)")]
    [SerializeField] private float openVolume = 1f;

    [Tooltip("เสียงตอนประตูปิด")]
    [SerializeField] private AudioClip closeSound;

    [Range(0f, 1f)]
    [Tooltip("ระดับความดังเสียงตอนประตูปิด (0 - 1)")]
    [SerializeField] private float closeVolume = 1f;

    [Tooltip("เสียงตอนประตูแตก (จะทำงานเฉพาะเมื่อประตูพัง/แตกจริงๆ เท่านั้น)")]
    [SerializeField] private AudioClip breakSound;

    [Range(0f, 1f)]
    [Tooltip("ระดับความดังเสียงตอนประตูแตก (0 - 1)")]
    [SerializeField] private float breakVolume = 1f;

    [Tooltip("AudioSource สำหรับเล่นเสียง (ถ้าเว้นว่างจะค้นหาจากตัวประตูให้อัตโนมัติ)")]
    [SerializeField] private AudioSource audioSource;
    #endregion

    #region State
    public bool IsOpen { get; private set; }
    public bool IsBroken { get; private set; }
    public bool IsBreakable => isBreakable;

    /// <summary>สลับเปิด/ปิดประตูจากภายนอก (เช่น เลเซอร์ของแพะ)</summary>
    public void RemoteToggle()
    {
        if (IsBroken) return;
        bool wasOpen = IsOpen;
        IsOpen = !IsOpen;
        if (IsOpen != wasOpen)
        {
            if (IsOpen) PlayOpenSound();
            else PlayCloseSound();
        }
        ApplyState();
    }

    private Vector3 closedPos;
    private Vector3 openPos;
    private bool positionsCached = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (blockingCollider == null) blockingCollider = GetComponent<Collider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        CachePositions();
        IsOpen = startsOpen;
        Physics2D.SyncTransforms();
        transform.position = ApplySupport(TargetPosition); // เริ่มที่จุดของสถานะเริ่มต้นเลย ไม่ต้องเลื่อน
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
        if (IsBroken) return;

        Vector3 target = ApplySupport(TargetPosition);
        if (transform.position != target)
        {
            transform.position = (slideSpeed > 0f)
                ? Vector3.MoveTowards(transform.position, target, slideSpeed * Time.deltaTime)
                : target;
        }

        // ปิดการชนเมื่อเลื่อนถึงจุดเปิดแล้วเท่านั้น (ตอนกำลังเลื่อนยังขวางอยู่)
        if (blockingCollider != null)
        {
            bool arrivedOpen = IsOpen && (slideSpeed <= 0f || Vector3.Distance(transform.position, openPos) <= 0.05f);
            blockingCollider.enabled = !(disableColliderWhenOpen && arrivedOpen);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckCollisionBreak(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckCollisionBreak(other.gameObject);
    }
    #endregion

    #region Positions
    private Vector3 TargetPosition => IsOpen ? openPos : closedPos;

    /// <summary>
    /// ถ้าประตูกำลังปิดลง และมีกล่องค้ำอยู่ใต้ประตู (ยังไม่พัง และทับซ้อนกับประตูในแนวนอน)
    /// ให้หยุดประตูไว้ที่หัวกล่อง แทนที่จะลงไปถึงจุดปิด
    /// </summary>
    private Vector3 ApplySupport(Vector3 target)
    {
        if (IsOpen || supportBoxes == null || supportBoxes.Length == 0) return target;
        if (blockingCollider == null || !blockingCollider.enabled) return target;

        Bounds door = blockingCollider.bounds;
        float bottomOffset = transform.position.y - door.min.y; // ระยะจากจุดกึ่งกลางประตูถึงขอบล่าง
        float holdY = float.NegativeInfinity;

        foreach (var box in supportBoxes)
        {
            if (box == null || box.IsBroken || !box.gameObject.activeInHierarchy) continue;
            var boxCol = box.GetComponent<Collider2D>();
            if (boxCol == null || !boxCol.enabled) continue;

            Bounds b = boxCol.bounds;
            if (b.min.x >= door.max.x || b.max.x <= door.min.x) continue; // ไม่ได้อยู่ใต้ประตู
            holdY = Mathf.Max(holdY, b.max.y + bottomOffset + supportGap);
        }

        if (holdY > target.y) target.y = holdY;
        return target;
    }

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
        if (IsBroken) return;

        Debug.Log("สวิตช์ส่งสัญญาณมาที่ประตูแล้ว! สถานะสวิตช์คือ: " + changed.IsActive);
        if (changed.IsActive)
        {
            if (isBreakable && breakOnSwitch)
            {
                BreakDoor();
                return;
            }

        }

        // ประตูตามสถานะสวิตช์: สวิตช์เปิด (ปุ่มยุบ) = ประตูเปิด / สวิตช์ปิด (ปุ่มเด้ง) = ประตูปิด
        bool wasOpen = IsOpen;
        IsOpen = CheckAllSwitchesTriggered();
        if (IsOpen != wasOpen)
        {
            if (IsOpen) PlayOpenSound();
            else PlayCloseSound();
        }
        ApplyState();
    }

    private void CheckCollisionBreak(GameObject hitObj)
    {
        if (!isBreakable || !breakOnCollision || IsBroken) return;

        TestAnimal animal = hitObj.GetComponentInParent<TestAnimal>();
        if (animal != null)
        {
            if (!CanAnimalBreakDoor(animal)) return;

            BreakDoor();
            return;
        }

        PushableBox box = hitObj.GetComponentInParent<PushableBox>();
        if (box != null)
        {
            BreakDoor();
            return;
        }
    }

    /// <summary>
    /// ตรวจสอบว่าสัตว์ตัวนี้มีสิทธิ์และได้รับยาที่มีความสามารถทำลายประตูนี้ด้วยการชนหรือไม่
    /// </summary>
    public bool CanAnimalBreakDoor(TestAnimal animal)
    {
        if (animal == null) return false;
        if (requirePoweredAnimal && !animal.CanUseAbility) return false;

        // 1. ถ้าเป็นตัวบิน (FlyingAnimal)
        if (animal is FlyingAnimal flyer)
        {
            if (!canBeBrokenByFlyer) return false;
            // ถ้าเปิด requireBreakAbility: ตัวบินต้องพุ่งชนด้วยความเร็วสูง (IsRamming จากยา Surge) เท่านั้น
            return !requireBreakAbility || flyer.IsRamming;
        }

        // 2. ถ้าเป็นแพะ (PusherAnimal)
        if (animal is PusherAnimal pusher)
        {
            if (!canBeBrokenByPusher) return false;
            // ถ้าเปิด requireBreakAbility: แพะต้องอยู่ในโหมดทุบ (CanSmash) เท่านั้น
            return !requireBreakAbility || pusher.CanSmash;
        }

        // 3. สัตว์ตัวอื่นๆ
        if (requireBreakAbility)
        {
            return animal.CanBreakObstacles;
        }

        return true;
    }

    /// <summary>
    /// สั่งให้ประตูพัง (ปลดบล็อกทางเดิน, ปิด Collider, เล่นเอฟเฟกต์)
    /// </summary>
    public void BreakDoor()
    {
        if (IsBroken) return;
        IsBroken = true;
        IsOpen = true;

        PlayBreakSound();

        Debug.Log($"[Door] ประตู {name} พังแล้ว!");

        if (blockingCollider != null)
        {
            blockingCollider.enabled = false;
        }

        if (breakEffectPrefab != null)
        {
            Instantiate(breakEffectPrefab, transform.position, Quaternion.identity);
        }

        if (animator != null)
        {
            animator.enabled = false;
        }

        if (brokenSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = brokenSprite;
        }
        else if (destroyOnBreak)
        {
            Destroy(gameObject);
        }
        else if (spriteRenderer != null)
        {
            // ถ้าไม่มี brokenSprite ให้ซ่อนตัวประตูเพื่อให้เห็นว่าเป็นทางผ่านโล่ง
            spriteRenderer.enabled = false;
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

    private void PlayOpenSound()
    {
        AudioClip clip = openSound != null ? openSound : (AudioManager.Instance != null ? AudioManager.Instance.GetClip("DoorOpen") : null);
        if (clip == null) return;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFXAt(clip, transform.position, openVolume);
        else if (audioSource != null) audioSource.PlayOneShot(clip, openVolume);
        else AudioSource.PlayClipAtPoint(clip, transform.position, openVolume);
    }

    private void PlayCloseSound()
    {
        AudioClip clip = closeSound != null ? closeSound : (AudioManager.Instance != null ? AudioManager.Instance.GetClip("DoorClose") : null);
        if (clip == null) return;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFXAt(clip, transform.position, closeVolume);
        else if (audioSource != null) audioSource.PlayOneShot(clip, closeVolume);
        else AudioSource.PlayClipAtPoint(clip, transform.position, closeVolume);
    }

    private void PlayBreakSound()
    {
        AudioClip clip = breakSound != null ? breakSound : (AudioManager.Instance != null ? AudioManager.Instance.GetClip("DoorBreak") : null);
        if (clip == null) return;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFXAt(clip, transform.position, breakVolume);
        else if (audioSource != null) audioSource.PlayOneShot(clip, breakVolume);
        else AudioSource.PlayClipAtPoint(clip, transform.position, breakVolume);
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
