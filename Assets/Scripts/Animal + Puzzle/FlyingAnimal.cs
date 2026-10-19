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
    [Header("Skyward (เขียว+ฟ้า) & Havoc (เขียว+ส้ม): เด้งสะท้อนแล้วบินไปหยุดที่จุดเป้าหมาย")]
    [Tooltip("จำนวนครั้งที่เด้งก่อนบินไปจุดเป้าหมาย")]
    [SerializeField] private int dizzyBounces = 6;

    [FormerlySerializedAs("teleportTarget")]
    [Tooltip("จุดเป้าหมายที่จะให้บินไปหยุดหลังเด้งครบจำนวน (Skyward / Havoc)")]
    [SerializeField] private Transform targetDestination;

    [Tooltip("(ไม่บังคับ) จุดเป้าหมายแยกเฉพาะสำหรับ Skyward ถ้าไม่ใส่จะใช้ targetDestination ร่วมกัน")]
    [SerializeField] private Transform skywardDestination;

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

    /// <summary>บินพุ่งไปข้างหน้าและเด้งสะท้อนกำแพงซ้าย-ขวาเหมือนกระจกทันที เพิ่มความเร็วทุกครั้งที่เด้ง และเมื่อครบจะวาร์ป (TP) หรือเด้งต่อ</summary>
    private IEnumerator BounceRoutine(int bounceCount, bool allowTeleportAfter, Vector2? customDestination = null)
    {
        isBouncing = true;
        TakeOff();
        flyEndTime = float.MaxValue;

        // กำหนดทิศทางเริ่มต้น: ให้พุ่งไปแนวนอน (ซ้าย-ขวา) เป็นหลัก
        float dirX = 0f;
        if (customDestination.HasValue)
        {
            dirX = customDestination.Value.x - rb.position.x;
        }
        if (Mathf.Abs(dirX) < 0.1f)
        {
            dirX = ToVector(flyDirection).x;
        }
        if (Mathf.Abs(dirX) < 0.1f)
        {
            dirX = FacingDirection.x;
        }
        if (Mathf.Abs(dirX) < 0.1f)
        {
            dirX = 1f; // ถ้าเป็น 0 หรือตั้งตรง ให้พุ่งไปทางขวาเป็นค่าเริ่มต้น
        }

        currentBounceDir = new Vector2(Mathf.Sign(dirX), 0f);
        FacingDirection = currentBounceDir;

        wallBouncedCount = 0;
        StopMoving(); // หยุดระบบเดินปกติของ TestAnimal เพราะตอนเด้งจะคุม Rigidbody โดยตรงใน FixedUpdate

        float speed = moveSpeed * SpeedMultiplier;
        rb.linearVelocity = new Vector2(currentBounceDir.x * speed, 0f) + ConveyorVelocity;

        // บินสะท้อนกำแพงไปเรื่อยๆ จนครบจำนวนครั้งที่กำหนด
        while (bounceCount <= 0 || wallBouncedCount < bounceCount)
        {
            yield return null;
        }

        isBouncing = false;
        rb.linearVelocity = Vector2.zero;

        Transform destTransform = (lastDrunkPotion == PotionType.Skyward && skywardDestination != null)
            ? skywardDestination
            : targetDestination;

        Vector3? targetPos = null;
        if (destTransform != null)
        {
            targetPos = destTransform.position;
        }
        else if (customDestination.HasValue)
        {
            targetPos = customDestination.Value;
        }

        if (targetPos.HasValue)
        {
            // บินพุ่งตรงไปยังจุดเป้าหมายที่กำหนด (แบบไม่ TP)
            Vector2 dest = targetPos.Value;
            float timeout = 4f;
            float elapsed = 0f;

            while (Vector2.Distance(rb.position, dest) > stopDistance && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                Vector2 dirToTarget = (dest - rb.position).normalized;
                FacingDirection = dirToTarget;

                float travelSpeed = Mathf.Max(moveSpeed * flySpeedMultiplier, moveSpeed * SpeedMultiplier);
                rb.linearVelocity = dirToTarget * travelSpeed + ConveyorVelocity;

                if (spriteRenderer != null && Mathf.Abs(dirToTarget.x) > 0.01f)
                {
                    bool goingLeft = dirToTarget.x < 0f;
                    spriteRenderer.flipX = spriteFacesRight ? goingLeft : !goingLeft;
                }

                yield return null;
            }

            // ถึงจุดหมายแล้ว: หยุดนิ่งและลงพื้น
            rb.position = dest;
            StopMoving();
            rb.linearVelocity = Vector2.zero;
            if (isFlying) Land();
        }
        else
        {
            // ถ้าไม่ได้กำหนดจุดเป้าหมาย ให้หยุดตรงจุดที่เด้งเสร็จและลงพื้น
            StopMoving();
            rb.linearVelocity = Vector2.zero;
            if (isFlying) Land();
        }
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

    /// <summary>ฟังก์ชันสะท้อนออกจากกำแพงซ้าย-ขวาทันทีแบบกระจก (ไม่ติดการเด้งบนล่าง)</summary>
    private void ReflectHorizontal(float normalX)
    {
        float signNormal = Mathf.Sign(normalX);

        // ตรวจสอบว่าทิศทางกำลังพุ่งเข้าหากำแพงจริงถึงจะสะท้อน (ป้องกันการสะท้อนซ้ำ)
        if (currentBounceDir.x * signNormal < 0f)
        {
            currentBounceDir = new Vector2(signNormal, 0f);
            FacingDirection = currentBounceDir;

            OnBounce();
            wallBouncedCount++;

            // ดันตัวออกจากกำแพงเล็กน้อยเพื่อไม่ให้ Collider ฝังในกำแพง
            rb.position = new Vector2(rb.position.x + signNormal * 0.05f, rb.position.y);

            float speed = moveSpeed * SpeedMultiplier;
            rb.linearVelocity = new Vector2(currentBounceDir.x * speed, 0f) + ConveyorVelocity;
        }
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

        // โหมดเด้ง (Havoc): ชนกำแพงซ้าย-ขวาแล้วเด้งสะท้อนกลับทันที (ไม่สะท้อนบนล่าง)
        if (isFlying && isBouncing)
        {
            Vector2 normal = Vector2.zero;
            int count = collision.contactCount;
            if (count > 0)
            {
                for (int i = 0; i < count; i++) normal += collision.contacts[i].normal;
                normal.Normalize();
            }
            if (Mathf.Abs(normal.x) > 0.3f)
            {
                ReflectHorizontal(normal.x);
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
            if (Mathf.Abs(normal.x) > 0.3f)
            {
                ReflectHorizontal(normal.x);
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
    private FlyMode mode = FlyMode.Normal;
    private PotionType lastDrunkPotion = PotionType.None;
    private bool isFlying = false;
    private bool isBouncing = false;
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
        if (isBouncing && spriteRenderer != null && Mathf.Abs(currentBounceDir.x) > 0.01f)
        {
            bool goingLeft = currentBounceDir.x < 0f;
            spriteRenderer.flipX = spriteFacesRight ? goingLeft : !goingLeft;
        }
    }

    protected override void FixedUpdate()
    {
        if (isBouncing)
        {
            float speed = moveSpeed * SpeedMultiplier;

            // ตรวจจับกำแพงซ้าย-ขวาล่วงหน้าแบบเรียลไทม์ด้วย CircleCast เพื่อสะท้อนออกทันที
            CircleCollider2D circle = bodyCollider as CircleCollider2D;
            float radius = circle != null ? circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * 0.95f : 0.45f;
            float castDist = speed * Time.fixedDeltaTime + 0.05f;

            RaycastHit2D hit = Physics2D.CircleCast(rb.position, radius, currentBounceDir, castDist, ~flyOverLayers);
            if (hit.collider != null && !hit.collider.isTrigger && hit.collider.gameObject != gameObject)
            {
                if (Mathf.Abs(hit.normal.x) > 0.3f)
                {
                    ReflectHorizontal(hit.normal.x);
                }
            }
            else
            {
                rb.linearVelocity = new Vector2(currentBounceDir.x * speed, 0f) + ConveyorVelocity;
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