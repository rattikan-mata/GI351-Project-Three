using System.Collections;
using UnityEngine;

/// <summary>ชนิดยา แต่ละตัวต้องใช้ยาของตัวเองถึงจะเริ่มทำงานได้ (ถ้าเปิด Require Potion)</summary>
public enum PotionType
{
    None,
    Vomit,      // ยาอาเจียน     -> ตัวอ้วกก้อนขน
    Light,      // ยาตัวเบา      -> ตัวอ้วนบิน
    Strength    // ยาเพิ่มพละกำลัง -> ตัวผลัก/ดันกล่อง
}

/// <summary>ทิศที่เลือกได้ใน Inspector สำหรับโหมดอัตโนมัติ</summary>
public enum AutoDirection
{
    Right, Left, Up, Down,
    UpRight, UpLeft, DownRight, DownLeft
}

/// <summary>
/// คลาสฐานของสัตว์ทดลองทั้ง 3 ตัว (HairballAnimal / FlyingAnimal / PusherAnimal)
/// จัดการระบบกลางที่ใช้ร่วมกัน: เดิน, เลือก/ยกเลิกเลือก, ความสามารถ + cooldown, ระบบยา, โหมดอัตโนมัติ
/// ความสามารถเฉพาะตัวให้คลาสลูก override PerformAbility() และ AutoRoutine()
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public abstract class TestAnimal : MonoBehaviour
{
    #region Identity
    [Header("Identity")]
    [SerializeField] protected string animalName = "Animal";
    public string AnimalName => animalName;
    #endregion

    #region Auto Mode
    [Header("Auto Mode (กินยาแล้วทำเองอัตโนมัติ)")]
    [Tooltip("ติ๊กไว้ = สัตว์ทำงานเอง ไม่รับคำสั่งคลิก/ปุ่ม\n" +
             "ถ้าติ๊ก Require Potion ด้วย = รอได้รับยาก่อนถึงเริ่ม\n" +
             "ถ้าไม่ติ๊ก Require Potion = เริ่มเองตอนเริ่มเกม")]
    [SerializeField] protected bool autoMode = true;

    [Tooltip("หน่วงกี่วินาทีหลังได้ยา (หรือเริ่มเกม) ก่อนจะเริ่มทำงาน")]
    [SerializeField] protected float autoStartDelay = 0.5f;

    private bool autoRunning = false;

    /// <summary>ผู้เล่นสั่งเองได้ไหม (ถ้าอยู่โหมดอัตโนมัติจะสั่งไม่ได้)</summary>
    public bool AcceptsManualInput => !autoMode;

    /// <summary>กำลังทำงานอัตโนมัติอยู่ไหม (Dropper ใช้รอให้สัตว์ทำเสร็จก่อนปล่อยยาตัวถัดไป)</summary>
    public bool IsAutoRunning => autoRunning;
    #endregion

    #region Movement
    [Header("Movement")]
    [Tooltip("ความเร็วเดิน")]
    [SerializeField] protected float moveSpeed = 3f;

    [Tooltip("ระยะห่างจากจุดหมายที่ถือว่าถึงแล้ว (หยุดเดิน)")]
    [SerializeField] protected float stopDistance = 0.1f;

    [Tooltip("ถ้าเดินแล้วไม่คืบหน้า (ติดกำแพง/ของขวาง) นานเท่านี้ (วินาที) จะหยุดเดินเอง")]
    [SerializeField] protected float stuckTimeout = 0.5f;

    [Tooltip("ระยะที่ต้องขยับได้อย่างน้อยในช่วง Stuck Timeout ไม่งั้นถือว่าติด")]
    [SerializeField] protected float stuckMinProgress = 0.1f;

    protected Rigidbody2D rb;
    private Vector2 targetPosition;
    private bool hasTarget = false;
    private float stuckTimer = 0f;
    private Vector2 stuckCheckPos;

    /// <summary>ทิศที่กำลังเดินอยู่ตอนนี้ (ศูนย์ถ้าหยุดอยู่)</summary>
    public Vector2 MoveDirection { get; private set; } = Vector2.zero;

    /// <summary>ทิศที่หันหน้าล่าสุด (คงค่าไว้แม้หยุดเดิน) ใช้ยิง/อ้วกไปข้างหน้า</summary>
    public Vector2 FacingDirection { get; protected set; } = Vector2.right;

    public bool IsMoving => hasTarget;

    /// <summary>ตัวคูณความเร็ว ให้คลาสลูก override ได้ (เช่น ตอนบินเร็วขึ้น)</summary>
    protected virtual float SpeedMultiplier => 1f;

    /// <summary>ลอยอยู่กลางอากาศไหม (สวิตช์ใช้เช็คว่าตัวเบาเกินกดไม่ติด)</summary>
    public virtual bool IsAirborne => false;

    /// <summary>ความเร็วจากสายพาน (ConveyorBelt ใส่ให้ แมวยังเดินเองได้พร้อมถูกพาไป)</summary>
    public Vector2 ConveyorVelocity { get; set; }
    #endregion

    #region Selection Visual & Sprites
    [Header("Selection Visual & Sprites")]
    [Tooltip("รูปสไปรต์แมวเปล่า/ร่างปกติก่อนกินยา")]
    [SerializeField] protected Sprite defaultCatSprite;

    [Tooltip("รูปสไปรต์ร่างใหม่หลังกินยา (ถ้าเว้นว่างไว้ จะใช้รูปเดิมที่ติดมากับ SpriteRenderer)")]
    [SerializeField] protected Sprite poweredUpSprite;

    [Tooltip("(ไม่บังคับ) GameObject วงแหวน/ลูกศรที่จะโชว์เหนือตัวที่ถูกเลือกอยู่")]
    [SerializeField] protected GameObject selectionIndicator;

    [Tooltip("กลับด้านสไปรต์ตามทิศที่เดิน")]
    [SerializeField] protected bool flipSpriteByDirection = true;

    [Tooltip("ติ๊กไว้ = สไปรต์ต้นฉบับหันไปทางขวา / ไม่ติ๊ก = หันไปทางซ้าย")]
    [SerializeField] protected bool spriteFacesRight = true;

    protected SpriteRenderer spriteRenderer;
    public bool IsSelected { get; private set; }
    #endregion

    #region Ability (Common)
    [Header("Ability (ใช้ร่วมกัน)")]
    [Tooltip("cooldown ของความสามารถ (วินาที)")]
    [SerializeField] protected float abilityCooldown = 1f;
    private float lastAbilityTime = -999f;
    #endregion

    #region Potion
    [Header("Potion (ระบบยา)")]
    [Tooltip("ไม่ติ๊ก = ไม่ต้องกินยา (เริ่มทำงานเองตอนเริ่มเกม ใช้ทดสอบ)\nติ๊ก = ต้องเรียก DrinkPotion() ด้วยยาที่ตรงกับตัวนี้ก่อน ถึงจะเริ่มทำงาน")]
    [SerializeField] protected bool requirePotion = false;

    [Tooltip("ผลของยาอยู่นานกี่วินาที (0 = ถาวร ไม่หมดฤทธิ์)")]
    [SerializeField] protected float potionEffectDuration = 0f;

    private float abilityUnlockedUntil = 0f;

    /// <summary>ยาที่ตัวนี้ต้องใช้ (แต่ละคลาสลูกกำหนดเอง)</summary>
    protected abstract PotionType RequiredPotion { get; }
    public PotionType RequiredPotionType => RequiredPotion;

    public bool CanUseAbility => !requirePotion || Time.time < abilityUnlockedUntil;

    /// <summary>ให้ยาสัตว์ตัวนี้ คืนค่า true ถ้าเป็นยาที่ถูกตัว ถ้าอยู่โหมดอัตโนมัติ สัตว์จะเริ่มทำงานเอง</summary>
    public bool DrinkPotion(PotionType potion)
    {
        if (potion != RequiredPotion) return false;

        abilityUnlockedUntil = (potionEffectDuration > 0f) ? Time.time + potionEffectDuration : float.MaxValue;
        Debug.Log($"[{animalName}] ใช้ยา {potion} แล้ว");

        // --- เปลี่ยนร่างเป็นสไปรต์ใหม่หลังได้รับยา ---
        if (spriteRenderer != null && poweredUpSprite != null)
        {
            spriteRenderer.sprite = poweredUpSprite;
        }

        TriggerAuto();
        return true;
    }
    #endregion

    #region Unity Lifecycle
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        // กัน Rigidbody หลับ (sleep) ตอนยืนนิ่ง ไม่งั้นสวิตช์จะไม่รู้ว่ายืนทับอยู่
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // --- ตั้งค่าสไปรต์เริ่มต้นเป็นแมวเปล่าตอนเริ่มเกม ---
        if (spriteRenderer != null && defaultCatSprite != null)
        {
            spriteRenderer.sprite = defaultCatSprite;
        }

        if (selectionIndicator != null) selectionIndicator.SetActive(false);
    }

    protected virtual void Start()
    {
        // ไม่ต้องใช้ยา = เริ่มทำงานเองเลย
        if (autoMode && !requirePotion) TriggerAuto();
    }

    protected virtual void Update()
    {
        if (flipSpriteByDirection && spriteRenderer != null && hasTarget && Mathf.Abs(MoveDirection.x) > 0.01f)
        {
            bool goingLeft = MoveDirection.x < 0f;
            spriteRenderer.flipX = spriteFacesRight ? goingLeft : !goingLeft;
        }
    }

    protected virtual void FixedUpdate()
    {
        Vector2 walk = Vector2.zero;

        if (hasTarget)
        {
            Vector2 toTarget = targetPosition - rb.position;
            if (toTarget.magnitude <= stopDistance)
            {
                StopMoving();
            }
            else
            {
                MoveDirection = toTarget.normalized;
                FacingDirection = MoveDirection;
                walk = MoveDirection * moveSpeed * SpeedMultiplier;

                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer >= stuckTimeout)
                {
                    if (Vector2.Distance(rb.position, stuckCheckPos) < stuckMinProgress)
                        StopMoving();
                    else
                    {
                        stuckTimer = 0f;
                        stuckCheckPos = rb.position;
                    }
                }
            }
        }

        rb.linearVelocity = walk + ConveyorVelocity;
    }
    #endregion

    #region Movement API
    /// <summary>สั่งให้เดินไปยังจุดนี้</summary>
    public void MoveTo(Vector2 worldPosition)
    {
        targetPosition = worldPosition;
        hasTarget = true;
        stuckTimer = 0f;
        stuckCheckPos = rb.position;
    }

    public void StopMoving()
    {
        hasTarget = false;
        MoveDirection = Vector2.zero;
        rb.linearVelocity = ConveyorVelocity;
    }
    #endregion

    #region Selection API
    public void Select()
    {
        IsSelected = true;
        if (selectionIndicator != null) selectionIndicator.SetActive(true);
    }

    public void Deselect()
    {
        IsSelected = false;
        if (selectionIndicator != null) selectionIndicator.SetActive(false);
    }
    #endregion

    #region Ability API
    /// <summary>ผู้เล่นสั่งใช้ความสามารถเอง (ใช้ไม่ได้ถ้าอยู่โหมดอัตโนมัติ)</summary>
    public bool UseAbility(Vector2 aimWorldPoint)
    {
        if (!AcceptsManualInput) return false;
        return ExecuteAbility(aimWorldPoint);
    }

    /// <summary>ใช้ความสามารถจริง (เช็คยา + cooldown) คลาสลูกเรียกใช้ในโหมดอัตโนมัติได้</summary>
    protected bool ExecuteAbility(Vector2 aimWorldPoint)
    {
        if (!CanUseAbility)
        {
            Debug.Log($"[{animalName}] ต้องใช้ยา {RequiredPotion} ก่อนถึงจะใช้ความสามารถได้");
            return false;
        }

        if (Time.time - lastAbilityTime < abilityCooldown) return false;

        if (PerformAbility(aimWorldPoint))
        {
            lastAbilityTime = Time.time;
            return true;
        }
        return false;
    }

    /// <summary>ความสามารถเฉพาะตัว คลาสลูก override เอง คืนค่า true ถ้าใช้สำเร็จ (เพื่อเริ่มนับ cooldown)</summary>
    protected abstract bool PerformAbility(Vector2 aimWorldPoint);
    #endregion

    #region Auto API
    /// <summary>เริ่มโหมดอัตโนมัติ (ถ้ากำลังทำอยู่แล้วจะไม่เริ่มซ้ำ)</summary>
    public void TriggerAuto()
    {
        if (!autoMode || autoRunning) return;
        if (!CanUseAbility) return;
        StartCoroutine(AutoWrapper());
    }

    private IEnumerator AutoWrapper()
    {
        autoRunning = true;
        yield return new WaitForSeconds(autoStartDelay);
        yield return AutoRoutine();
        autoRunning = false;
    }

    /// <summary>สิ่งที่สัตว์ทำเองหลังได้ยา คลาสลูก override เอง</summary>
    protected virtual IEnumerator AutoRoutine()
    {
        yield break;
    }

    protected static Vector2 ToVector(AutoDirection d)
    {
        switch (d)
        {
            case AutoDirection.Right: return Vector2.right;
            case AutoDirection.Left: return Vector2.left;
            case AutoDirection.Up: return Vector2.up;
            case AutoDirection.Down: return Vector2.down;
            case AutoDirection.UpRight: return new Vector2(1f, 1f).normalized;
            case AutoDirection.UpLeft: return new Vector2(-1f, 1f).normalized;
            case AutoDirection.DownRight: return new Vector2(1f, -1f).normalized;
            default: return new Vector2(-1f, -1f).normalized;
        }
    }
    #endregion
}