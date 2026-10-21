using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// สัตว์ทดลองตัวที่ 2: ตัวอ้วนบิน (ใช้ยาตัวเบา) = วัว
/// ความสามารถ: ลอยขึ้นกลางอากาศชั่วคราว ลอยข้ามสิ่งกีดขวาง/หลุม (Layer ที่ตั้งไว้) ได้ และเคลื่อนที่เร็วขึ้น
/// ตอนลอยตัวเบาเกินจะไม่กดสวิตช์ (สวิตช์ตั้งให้ข้ามตัวที่ลอยอยู่ได้)
/// โหมดอัตโนมัติ: ได้ยาแล้วลอยขึ้น บินไปทิศที่ตั้งตามระยะที่กำหนด แล้วลงพื้นเอง
/// ยาผสม:
///   Skyward (เขียว+ฟ้า) = เด้งสะท้อนแนวนอน เพิ่มความเร็วทุกครั้งที่เด้ง แล้วบินไปหยุดที่จุดเป้าหมาย
///   Havoc   (เขียว+ส้ม) = เด้งสะท้อนแนวนอน เพิ่มความเร็วทุกครั้งที่เด้ง แล้วบินไปหยุดที่จุดเป้าหมาย
///   Surge   (ฟ้า+ส้ม)   = บินพุ่งชนกล่อง/ประตูให้พังได้
/// </summary>
public class FlyingAnimal : TestAnimal
{
    private enum FlyMode { Normal, Zigzag, Dizzy, Ram }

    #region Flying Settings
    [Header("Flying (บิน)")]
    [Tooltip("ลอยอยู่นานกี่วินาทีก่อนตกลงพื้นเอง (โหมดคุมเอง)")]
    [SerializeField] private float flyDuration = 4f;

    [Tooltip("ตัวคูณความเร็วเดินตอนลอย")]
    [SerializeField] private float flySpeedMultiplier = 1.3f;

    [Tooltip("Layer ที่ลอยข้ามได้ตอนบิน (เช่น หลุม น้ำ กำแพงเตี้ย) เลือกเฉพาะ Layer ที่อยากให้บินข้าม")]
    [SerializeField] private LayerMask flyOverLayers;
    #endregion

    #region Combo Settings
    [Header("Skyward & Havoc: จุดรีเซ็ท & จุดเป้าหมาย")]
    [Tooltip("จุดรีเซ็ทกลางทาง 1: เมื่อสุ่มเด้งเกิน (2-3 ครั้ง) แล้วยังไม่ถึงเป้าหมาย จะเด้งกลับมาเริ่มที่จุดนี้")]
    [SerializeField] private Transform midwayResetPoint;

    [FormerlySerializedAs("teleportTarget")]
    [Tooltip("จุดเป้าหมาย 1: จุดที่ต้องการให้เด้งไปให้ถึง เมื่อถึงแล้วจะปล่อยให้เด้งอิสระ")]
    [SerializeField] private Transform targetDestination;

    [Tooltip("(ไม่บังคับ) จุดเป้าหมายแยกเฉพาะสำหรับ Skyward ถ้าไม่ใส่จะใช้ targetDestination ร่วมกัน")]
    [SerializeField] private Transform skywardDestination;

    [Tooltip("จำนวนครั้งเด้งขั้นต่ำก่อนรีเซ็ทกลับจุดกลางทาง")]
    [SerializeField] private int minBouncesBeforeReset = 2;

    [Tooltip("จำนวนครั้งเด้งสูงสุดก่อนรีเซ็ทกลับจุดกลางทาง")]
    [SerializeField] private int maxBouncesBeforeReset = 3;

    [Header("Free Bounce (เด้งอิสระหลังทำเป้าหมายเสร็จ)")]
    [Tooltip("ระยะเวลาเด้งอิสระหลังทำเป้าหมายสำเร็จ (วินาที)")]
    [SerializeField] private float freeBounceDuration = 8f;

    [Tooltip("จำนวนครั้งที่เด้งอิสระหลังทำเป้าหมายสำเร็จ (0 = เด้งตามเวลา freeBounceDuration)")]
    [SerializeField] private int freeBounces = 10;

    [SerializeField, HideInInspector] private int dizzyBounces = 6;
    [SerializeField, HideInInspector] private float zigzagStep = 1.5f;
    [SerializeField, HideInInspector] private float zigzagAmplitude = 1.2f;
    [SerializeField, HideInInspector] private int zigzagMaxLegs = 20;

    [Tooltip("(ไม่บังคับ) Prefab ควันตอนวาร์ป/หายตัว")]
    [SerializeField] private GameObject smokeEffectPrefab;

    [Header("Bounce Speed (ความเร็วตอนเด้ง)")]
    [Tooltip("ความเร็วที่เพิ่มขึ้นในแต่ละครั้งที่เด้ง")]
    [SerializeField] private float speedIncreasePerBounce = 1f;

    [Tooltip("ขีดจำกัดความเร็วสูงสุดที่เด้งได้ (Max Speed Limit)")]
    [SerializeField] private float maxBounceSpeed = 12f;

    [Header("Surge (ฟ้า+ส้ม): บินพุ่งชน")]
    [Tooltip("ตัวคูณความเร็วตอนพุ่งชน")]
    [SerializeField] private float ramSpeedMultiplier = 2.5f;

    [Tooltip("ระยะที่พุ่งไป")]
    [SerializeField] private float ramDistance = 8f;

    [Tooltip("Layer ที่ตัวพุ่งชนได้/ชนแล้วหยุด (เช่น กล่อง ประตู กำแพง) Layer อื่นจะทะลุผ่านไปตอนพุ่ง\nเว้นว่าง (Nothing) = ชนทุก Layer ตามปกติ")]
    [SerializeField] private LayerMask ramCollideLayers;
    #endregion

    #region Auto
    [Header("Auto Flight (โหมดอัตโนมัติ)")]
    [Tooltip("ทิศที่จะบินไปเมื่อได้ยา")]
    [SerializeField] private AutoDirection flyDirection = AutoDirection.Right;

    [Tooltip("ระยะที่บินไป (หน่วย Unity) ถึงแล้วจะลงพื้นเอง")]
    [SerializeField] private float flyDistance = 5f;

    protected override IEnumerator AutoRoutine()
    {
        switch (mode)
        {
            case FlyMode.Zigzag:
            case FlyMode.Dizzy: yield return BounceRoutine(dizzyBounces, true); break;
            case FlyMode.Ram: yield return RamRoutine(ToVector(flyDirection)); break;
            default: yield return NormalFlightRoutine(); break;
        }
    }

    private IEnumerator NormalFlightRoutine()
    {
        // ขึ้นบิน
        if (!ExecuteAbility(rb.position)) yield break;
        flyEndTime = float.MaxValue; // ไม่ให้ตกกลางทางเพราะหมดเวลา เดี๋ยวลงเองตอนถึงที่หมาย

        // บินไปทิศที่ตั้งไว้
        Vector2 dir = ToVector(flyDirection);
        MoveTo(rb.position + dir * flyDistance);
        while (IsMoving) yield return null;

        // ถึงแล้ว (หรือชนสิ่งกีดขวาง) ลงพื้น
        if (isFlying) Land();
    }

    private bool zigzagWallHit = false;

    /// <summary>บินซิกแซกไปข้างหน้า ถ้าชนสิ่งกีดขวางจะเด้งสะท้อนกลับทันที</summary>
    private IEnumerator ZigzagRoutine()
    {
        if (!ExecuteAbility(rb.position)) yield break;
        flyEndTime = float.MaxValue;

        Vector2 dir = ToVector(flyDirection);
        Vector2 perp = new Vector2(-dir.y, dir.x);
        int side = 1;
        float traveled = 0f;

        for (int leg = 0; leg < zigzagMaxLegs && traveled < flyDistance; leg++)
        {
            Vector2 start = rb.position;
            zigzagWallHit = false;
            MoveTo(start + dir * zigzagStep + perp * side * zigzagAmplitude);

            while (IsMoving && !zigzagWallHit) yield return null;

            float moved = Vector2.Dot(rb.position - start, dir);
            if (zigzagWallHit || moved < 0.05f)
            {
                // ชนสิ่งกีดขวาง -> เด้งสะท้อนกลับทันที ไม่ต้องรอค้างติดกำแพง
                dir = -dir;
                perp = -perp;
                OnBounce();
            }
            else
            {
                traveled += moved;
            }
            side = -side;
        }

        if (isFlying) Land();
    }

    /// <summary>บินและเด้งสะท้อนกำแพง โดยมีจุดรีเซ็ทกลางทาง สุ่มทิศและสุ่มเด้ง 2-3 ครั้ง หากไม่ถึงเป้าหมายจะเด้งกลับมารีเซ็ท เมื่อถึงเป้าหมายจะปล่อยเด้งอิสระ</summary>
    private IEnumerator BounceRoutine(int bounceCount, bool allowTeleportAfter, Vector2? customDestination = null)
    {
        isBouncing = true;
        TakeOff();
        flyEndTime = float.MaxValue;
        StopMoving();

        wallBouncedCount = 0;
        attemptBounceCount = 0;
        freeBounceCount = 0;
        freeBounceTimer = 0f;

        Transform destTransform = (lastDrunkPotion == PotionType.Skyward && skywardDestination != null)
            ? skywardDestination
            : targetDestination;

        if (midwayResetPoint != null)
        {
            float distToReset = Vector2.Distance(rb.position, midwayResetPoint.position);
            if (distToReset <= 0.6f)
            {
                SetupResetPointLaunch();
            }
            else
            {
                bounceStage = BounceStage.MovingToReset;
                currentBounceDir = ((Vector2)midwayResetPoint.position - rb.position).normalized;
                FacingDirection = currentBounceDir;
                float speed = moveSpeed * SpeedMultiplier;
                rb.linearVelocity = currentBounceDir * speed + ConveyorVelocity;
            }
        }
        else
        {
            SetupResetPointLaunch();
        }

        // ปล่อยให้ FixedUpdate จัดการทุกสเตจจนเสร็จสิ้น
        while (isBouncing)
        {
            yield return null;
        }

        isBouncing = false;
        StopMoving();
        rb.linearVelocity = Vector2.zero;
        if (isFlying) Land();
    }

    /// <summary>สุ่มทิศทางและสุ่มจำนวนครั้งที่เด้งได้ (2-3 ครั้ง) จากจุดรีเซ็ทกลางทาง</summary>
    private void SetupResetPointLaunch()
    {
        if (midwayResetPoint != null)
        {
            rb.position = midwayResetPoint.position;
        }

        attemptBounceCount = 0;
        allowedBouncesThisAttempt = Random.Range(minBouncesBeforeReset, maxBouncesBeforeReset + 1);

        Transform destTransform = (lastDrunkPotion == PotionType.Skyward && skywardDestination != null)
            ? skywardDestination
            : targetDestination;

        Vector2 toTarget;
        if (destTransform != null)
        {
            toTarget = (Vector2)destTransform.position - rb.position;
        }
        else
        {
            toTarget = ToVector(flyDirection);
        }

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            toTarget = Vector2.up;
        }

        // สุ่มมุมกระจายตัวจากทิศทางมุ่งหน้าไปหาเป้าหมาย (กระจายซ้าย-ขวา ±65 องศา)
        float baseAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float randomOffset = Random.Range(-65f, 65f);
        if (Mathf.Abs(randomOffset) < 15f)
        {
            randomOffset = Mathf.Sign(randomOffset != 0 ? randomOffset : (Random.value - 0.5f)) * 25f;
        }

        float launchAngle = (baseAngle + randomOffset) * Mathf.Deg2Rad;
        Vector2 launchDir = new Vector2(Mathf.Cos(launchAngle), Mathf.Sin(launchAngle)).normalized;

        currentBounceDir = launchDir;
        FacingDirection = launchDir;
        bounceStage = BounceStage.BouncingToTarget;

        float speed = moveSpeed * SpeedMultiplier;
        rb.linearVelocity = launchDir * speed + ConveyorVelocity;

        Debug.Log($"[{animalName}] ออกตัวจากจุดรีเซ็ท: สุ่มทิศ {launchDir}, กำหนดเด้งได้ {allowedBouncesThisAttempt} ครั้ง");
    }

    /// <summary>พุ่งชนไปข้างหน้าด้วยความเร็วสูง กล่อง/ประตูที่พังได้จะแตก</summary>
    private IEnumerator RamRoutine(Vector2 dir)
    {
        if (IsRamming) yield break;

        IsRamming = true;
        ramTarget = null;
        FacingDirection = dir;

        // ชนเฉพาะ Layer ที่ตั้งไว้ (ถ้าเว้นว่าง = ชนทุก Layer)
        if (ramCollideLayers.value != 0)
            bodyCollider.excludeLayers = (LayerMask)(~ramCollideLayers.value);

        MoveTo(rb.position + dir * ramDistance);

        // รอจนหยุดพุ่ง และรอกระบวนการ "กระเด็นถอย -> พุ่งกลับไปชนซ้ำ" ของกล่องให้จบ
        while (IsMoving || IsExternalMotion || (ramTarget != null && ramTarget.IsProcessingHit))
            yield return null;

        bodyCollider.excludeLayers = isFlying ? flyOverLayers : (LayerMask)0;
        IsRamming = false;
        ramTarget = null;
    }

    /// <summary>ฟังก์ชันสะท้อนออกจากกำแพงทันทีแบบกระจก (รองรับทุกมุมกำแพง)</summary>
    private void OnBounceHit(Vector2 normal)
    {
        if (normal.sqrMagnitude < 0.0001f) return;
        normal.Normalize();

        // ตรวจสอบว่าทิศทางกำลังพุ่งเข้าหากำแพงจริงถึงจะสะท้อน (ป้องกันการสะท้อนซ้ำ)
        if (Vector2.Dot(currentBounceDir, normal) < 0f)
        {
            currentBounceDir = Vector2.Reflect(currentBounceDir, normal).normalized;
            FacingDirection = currentBounceDir;

            // ดันตัวออกจากกำแพงเล็กน้อยเพื่อไม่ให้ Collider ฝังในกำแพง
            rb.position += normal * 0.05f;

            float speed = moveSpeed * SpeedMultiplier;
            rb.linearVelocity = currentBounceDir * speed + ConveyorVelocity;

            OnBounce();
            wallBouncedCount++;

            if (bounceStage == BounceStage.BouncingToTarget)
            {
                attemptBounceCount++;
                Debug.Log($"[{animalName}] เด้งครั้งที่ {attemptBounceCount}/{allowedBouncesThisAttempt}");

                // ถ้าเด้งเกิน (สุ่ม 2-3 ครั้ง) แล้วยังไม่ถึงเป้าหมาย -> เด้งกลับมาที่จุดรีเซ็ท
                if (attemptBounceCount >= allowedBouncesThisAttempt && midwayResetPoint != null)
                {
                    Debug.Log($"[{animalName}] เด้งเกินกำหนด ({allowedBouncesThisAttempt} ครั้ง) แล้วยังไม่ถึงเป้าหมาย -> เด้งกลับจุดรีเซ็ท");
                    bounceStage = BounceStage.ReturningToReset;
                    Vector2 toReset = ((Vector2)midwayResetPoint.position - rb.position).normalized;
                    currentBounceDir = toReset;
                    FacingDirection = currentBounceDir;
                    rb.linearVelocity = currentBounceDir * speed + ConveyorVelocity;
                }
            }
            else if (bounceStage == BounceStage.FreeBouncing)
            {
                freeBounceCount++;
            }
        }
    }

    private void ReflectHorizontal(float normalX)
    {
        OnBounceHit(new Vector2(normalX, 0f));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsRamming)
        {
            PushableBox box = collision.gameObject.GetComponentInParent<PushableBox>();
            if (box != null)
            {
                ramTarget = box;
                if (box.IsDestructible && !box.IsBroken)
                {
                    // กล่องจะทำให้ตัวพุ่งกระเด็นถอยแล้วพุ่งกลับไปชนซ้ำจนแตก (ตาม Hits To Break)
                    Vector2 hitDir = FacingDirection;
                    if (hitDir.sqrMagnitude < 0.01f) hitDir = ((Vector2)box.transform.position - rb.position).normalized;
                    box.TriggerDestructionSequence(this, hitDir);
                }
            }

            Door door = collision.gameObject.GetComponentInParent<Door>();
            if (door != null && door.IsBreakable && !door.IsBroken) door.BreakDoor();
            return;
        }

        // โหมดเด้ง: ชนกำแพงแล้วสะท้อนทันทีแบบกระจก
        if (isFlying && isBouncing)
        {
            Vector2 normal = Vector2.zero;
            int count = collision.contactCount;
            if (count > 0)
            {
                for (int i = 0; i < count; i++) normal += collision.contacts[i].normal;
                normal.Normalize();
            }
            if (normal.sqrMagnitude > 0.01f)
            {
                OnBounceHit(normal);
            }
            return;
        }

        // โหมดซิกแซก: ชนแล้วให้เด้งกลับทันที ไม่ต้องรอค้างติดกำแพง
        if (isFlying && mode == FlyMode.Zigzag)
        {
            zigzagWallHit = true;
            StopMoving();
            return;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isFlying && isBouncing && !IsRamming)
        {
            Vector2 normal = Vector2.zero;
            int count = collision.contactCount;
            if (count > 0)
            {
                for (int i = 0; i < count; i++) normal += collision.contacts[i].normal;
                normal.Normalize();
            }
            if (normal.sqrMagnitude > 0.01f)
            {
                OnBounceHit(normal);
            }
        }
    }
    #endregion

    #region Visual
    [Header("Visual")]
    [Tooltip("สไปรต์ลอยสูงขึ้นกี่หน่วยตอนบิน (ภาพเท่านั้น ไม่กระทบตำแหน่งจริง)")]
    [SerializeField] private float liftHeight = 0.5f;

    [Tooltip("ความเร็วที่สไปรต์ลอยขึ้น/ลงมา")]
    [SerializeField] private float liftSmoothSpeed = 8f;

    [Tooltip("(ไม่บังคับ) เงาใต้ตัว จะโชว์เฉพาะตอนลอย")]
    [SerializeField] private GameObject shadow;
    #endregion

    #region State
    private enum BounceStage
    {
        MovingToReset,    // เดินทางไปจุดรีเซ็ทกลางทาง
        BouncingToTarget, // สุ่มทิศจากจุดรีเซ็ท เด้งกำแพง (สุ่ม 2-3 ครั้ง) เพื่อพยายามไปให้ถึงเป้าหมาย
        ReturningToReset, // เด้งเกิน 2-3 ครั้งแล้วยังไม่ถึงเป้าหมาย -> เด้งกลับมาที่จุดรีเซ็ท
        FreeBouncing      // ทำเป้าหมายเสร็จแล้ว -> เด้งอิสระ
    }

    private FlyMode mode = FlyMode.Normal;
    private PotionType lastDrunkPotion = PotionType.None;
    private bool isFlying = false;
    private bool isBouncing = false;
    private BounceStage bounceStage = BounceStage.BouncingToTarget;
    private int attemptBounceCount = 0;
    private int allowedBouncesThisAttempt = 3;
    private int freeBounceCount = 0;
    private float freeBounceTimer = 0f;
    private Vector2 currentBounceDir;
    private int wallBouncedCount = 0;
    private Coroutine activeBounceRoutine;
    private float flyEndTime;
    private Collider2D bodyCollider;
    private PushableBox ramTarget;
    private Transform spriteTransform;
    private Vector3 spriteBaseLocalPos;
    private float currentBounceSpeedBonus = 0f;

    /// <summary>กำลังพุ่งชน (กล่องใช้เช็คว่าชนแล้วพังไหม)</summary>
    public bool IsRamming { get; private set; }

    /// <summary>หายตัวไปแล้ว (โหมด Havoc)</summary>
    public bool IsVanished { get; private set; }

    public override bool IsAirborne => isFlying;

    protected override float SpeedMultiplier
    {
        get
        {
            if (IsRamming) return ramSpeedMultiplier;
            if (isFlying)
            {
                float baseSpeed = moveSpeed * flySpeedMultiplier;
                float currentSpeed = Mathf.Clamp(baseSpeed + currentBounceSpeedBonus, baseSpeed, Mathf.Max(baseSpeed, maxBounceSpeed));
                return moveSpeed > 0f ? (currentSpeed / moveSpeed) : 1f;
            }
            return 1f;
        }
    }

    protected override PotionType RequiredPotion => PotionType.Light;

    public override bool AcceptsPotion(PotionType potion)
    {
        return potion == PotionType.Light || IsComboPotion(potion);
    }

    private void OnBounce()
    {
        currentBounceSpeedBonus += speedIncreasePerBounce;
    }

    protected override void OnPotionDrunk(PotionType potion)
    {
        currentBounceSpeedBonus = 0f;
        lastDrunkPotion = potion;

        switch (potion)
        {
            case PotionType.Skyward:
            case PotionType.Havoc:
                mode = FlyMode.Dizzy;
                break;
            case PotionType.Surge:
                mode = FlyMode.Ram;
                break;
            default:
                mode = FlyMode.Normal;
                break;
        }
        Debug.Log($"[{animalName}] โหมดบิน: {mode} (ยา: {potion})");
    }
    #endregion

    #region Unity Lifecycle
    protected override void Awake()
    {
        base.Awake();
        bodyCollider = GetComponent<Collider2D>();

        if (spriteRenderer != null)
        {
            if (spriteRenderer.transform == transform)
            {
                // สไปรต์อยู่บนตัวหลัก ถ้ายกสไปรต์จะยกตัวจริงไปด้วยและดึงตำแหน่งกลับ ทำให้บินไปไม่ได้
                Debug.LogWarning($"[{animalName}] SpriteRenderer อยู่บนตัวหลัก จึงปิดการยกสไปรต์ไว้ " +
                                 "ให้สร้างลูก (child) แล้วย้าย SpriteRenderer ไปไว้ที่ลูกถึงจะเห็นตัวลอยขึ้น");
            }
            else
            {
                spriteTransform = spriteRenderer.transform;
                spriteBaseLocalPos = spriteTransform.localPosition;
            }
        }
        if (shadow != null) shadow.SetActive(false);
    }

    private void OnDisable()
    {
        if (activeBounceRoutine != null)
        {
            StopCoroutine(activeBounceRoutine);
            activeBounceRoutine = null;
        }
    }

    protected override void Update()
    {
        base.Update();

        if (isFlying && Time.time >= flyEndTime) Land();

        // ยกสไปรต์ขึ้น/ลงแบบนุ่มๆ
        if (spriteTransform != null)
        {
            Vector3 target = spriteBaseLocalPos + (isFlying ? Vector3.up * liftHeight : Vector3.zero);
            spriteTransform.localPosition = Vector3.Lerp(spriteTransform.localPosition, target, liftSmoothSpeed * Time.deltaTime);
        }

        // หันหน้าซ้าย-ขวาตามทิศทางการเด้ง
        if (isBouncing && spriteRenderer != null && Mathf.Abs(FacingDirection.x) > 0.01f)
        {
            FaceHorizontal(FacingDirection.x < 0f);
        }
    }

    protected override void FixedUpdate()
    {
        if (isBouncing)
        {
            float speed = moveSpeed * SpeedMultiplier;
            float step = speed * Time.fixedDeltaTime;

            Transform destTransform = (lastDrunkPotion == PotionType.Skyward && skywardDestination != null)
                ? skywardDestination
                : targetDestination;

            // --- 1. สเตจ: เดินทางไปจุดรีเซ็ทกลางทาง (ถ้าเริ่มจากจุดอื่น) ---
            if (bounceStage == BounceStage.MovingToReset)
            {
                if (midwayResetPoint != null)
                {
                    Vector2 toReset = (Vector2)midwayResetPoint.position - rb.position;
                    if (toReset.magnitude <= Mathf.Max(stopDistance, 0.45f))
                    {
                        SetupResetPointLaunch();
                        return;
                    }
                    currentBounceDir = toReset.normalized;
                    FacingDirection = currentBounceDir;
                    rb.linearVelocity = currentBounceDir * speed + ConveyorVelocity;
                }
                else
                {
                    SetupResetPointLaunch();
                    return;
                }
            }

            // --- 2. สเตจ: เด้งกลับมาจุดรีเซ็ทกลางทาง (เมื่อเด้งเกิน 2-3 ครั้ง) ---
            else if (bounceStage == BounceStage.ReturningToReset)
            {
                if (midwayResetPoint != null)
                {
                    Vector2 toReset = (Vector2)midwayResetPoint.position - rb.position;
                    if (toReset.magnitude <= Mathf.Max(stopDistance, 0.45f))
                    {
                        // ถึงจุดรีเซ็ทแล้ว -> สุ่มทิศใหม่และสุ่มจำนวนเด้ง 2-3 ครั้ง แล้วพุ่งตัวอีกรอบ
                        SetupResetPointLaunch();
                        return;
                    }
                    currentBounceDir = toReset.normalized;
                    FacingDirection = currentBounceDir;
                    rb.linearVelocity = currentBounceDir * speed + ConveyorVelocity;
                }
                else
                {
                    SetupResetPointLaunch();
                    return;
                }
            }

            // --- 3. สเตจ: กำลังเด้งพยายามไปให้ถึงเป้าหมาย ---
            else if (bounceStage == BounceStage.BouncingToTarget)
            {
                // ตรวจสอบว่าถึงเป้าหมายหรือยัง
                if (destTransform != null)
                {
                    float distToTarget = Vector2.Distance(rb.position, destTransform.position);
                    float arrivalThreshold = Mathf.Max(stopDistance, step * 0.9f + 0.2f);

                    if (distToTarget <= arrivalThreshold)
                    {
                        // ถึงเป้าหมายแล้ว! (พอทำเป้าหมายเสร็จ)
                        Debug.Log($"[{animalName}] ถึงเป้าหมายสำเร็จแล้ว! ปล่อยให้เด้งอิสระ");

                        // ถ้าเป้าหมายมี FloorSwitch ให้กดสวิตช์ด้วย
                        FloorSwitch sw = destTransform.GetComponentInParent<FloorSwitch>();
                        if (sw != null) sw.RemoteToggle();

                        // เปลี่ยนสเตจเป็นเด้งอิสระ (FreeBouncing)
                        bounceStage = BounceStage.FreeBouncing;
                        freeBounceCount = 0;
                        freeBounceTimer = 0f;
                        if (currentBounceDir.sqrMagnitude < 0.01f) currentBounceDir = FacingDirection;
                        rb.linearVelocity = currentBounceDir * speed + ConveyorVelocity;
                        return;
                    }
                }

                FacingDirection = currentBounceDir;
                rb.linearVelocity = currentBounceDir * speed + ConveyorVelocity;
            }

            // --- 4. สเตจ: เด้งอิสระ (FreeBouncing) ---
            else if (bounceStage == BounceStage.FreeBouncing)
            {
                freeBounceTimer += Time.fixedDeltaTime;

                bool timeExpired = freeBounceDuration > 0f && freeBounceTimer >= freeBounceDuration;
                bool bouncesExceeded = freeBounces > 0 && freeBounceCount >= freeBounces;

                if (timeExpired || bouncesExceeded)
                {
                    Debug.Log($"[{animalName}] สิ้นสุดการเด้งอิสระ ลงจอดสู่พื้น");
                    rb.linearVelocity = Vector2.zero;
                    isBouncing = false;
                    StopMoving();
                    Land();
                    return;
                }

                FacingDirection = currentBounceDir;
                rb.linearVelocity = currentBounceDir * speed + ConveyorVelocity;
            }

            // ตรวจจับกำแพงล่วงหน้าแบบเรียลไทม์ด้วย CircleCast เพื่อสะท้อนออกทันทีแบบกระจก
            CircleCollider2D circle = bodyCollider as CircleCollider2D;
            float radius = circle != null ? circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * 0.95f : 0.45f;
            float castDist = step + 0.05f;

            RaycastHit2D hit = Physics2D.CircleCast(rb.position, radius, currentBounceDir, castDist, ~flyOverLayers);
            if (hit.collider != null && !hit.collider.isTrigger && hit.collider.gameObject != gameObject)
            {
                OnBounceHit(hit.normal);
                return;
            }

            return;
        }

        base.FixedUpdate();
    }
    #endregion

    #region Ability
    protected override bool PerformAbility(Vector2 aimWorldPoint)
    {
        if (mode == FlyMode.Ram)
        {
            Vector2 d = aimWorldPoint - rb.position;
            if (d.sqrMagnitude < 0.0001f) d = FacingDirection;
            StartCoroutine(RamRoutine(d.normalized));
            return true;
        }
        if (mode == FlyMode.Dizzy || mode == FlyMode.Zigzag)
        {
            if (activeBounceRoutine != null)
            {
                StopCoroutine(activeBounceRoutine);
                activeBounceRoutine = null;
            }

            activeBounceRoutine = StartCoroutine(BounceRoutine(dizzyBounces, true, aimWorldPoint));
            return true;
        }
        if (isFlying) Land();
        else TakeOff();
        return true;
    }

    private void TakeOff()
    {
        isFlying = true;
        flyEndTime = Time.time + flyDuration;
        bodyCollider.excludeLayers = flyOverLayers; // ไม่ชน Layer เหล่านี้ตอนลอย
        if (shadow != null) shadow.SetActive(true);
    }

    private void Land()
    {
        isFlying = false;
        isBouncing = false;
        currentBounceSpeedBonus = 0f;
        if (activeBounceRoutine != null)
        {
            StopCoroutine(activeBounceRoutine);
            activeBounceRoutine = null;
        }
        bodyCollider.excludeLayers = (LayerMask)0;
        if (shadow != null) shadow.SetActive(false);
    }

    private void TeleportTo(Vector3 destination)
    {
        // เอฟเฟกต์ควันตอนเริ่มวาร์ปจากจุดเดิม
        if (smokeEffectPrefab != null) Instantiate(smokeEffectPrefab, transform.position, Quaternion.identity);

        StopMoving();
        rb.position = destination;
        transform.position = destination;

        // เอฟเฟกต์ควันตอนโผล่มาที่จุดใหม่
        if (smokeEffectPrefab != null) Instantiate(smokeEffectPrefab, destination, Quaternion.identity);

        Debug.Log($"[{animalName}] ควอนตั้มวาร์ป (TP) ไปยังจุด {destination} สำเร็จ!");
    }

    private void Vanish()
    {
        if (smokeEffectPrefab != null) Instantiate(smokeEffectPrefab, transform.position, Quaternion.identity);

        IsVanished = true;
        StopMoving();
        foreach (var r in GetComponentsInChildren<SpriteRenderer>()) r.enabled = false;
        if (selectionIndicator != null) selectionIndicator.SetActive(false);
        if (shadow != null) shadow.SetActive(false);
        bodyCollider.enabled = false;
        Debug.Log($"[{animalName}] เวียนหัวจนหายตัวไป!");
    }
    #endregion
}