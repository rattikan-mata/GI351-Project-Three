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
    private enum FlyMode { Normal, Zigzag, Dizzy, Ram, PatrolPush, Havoc }

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

    #region Havoc Settings
    [Header("Havoc (เขียว+ส้ม): เด้งก่อนวาร์ป")]
    [Tooltip("จำนวนครั้งที่เด้งสะท้อนกำแพงก่อนวาร์ปไปยังเป้าหมาย (ค่าเริ่มต้น 5 ครั้ง)")]
    [SerializeField] private int havocBouncesBeforeTeleport = 5;

    [Tooltip("(ไม่บังคับ) จุดเป้าหมายแยกเฉพาะสำหรับ Havoc ถ้าไม่ใส่จะใช้ targetDestination / skywardDestination / Teleport point ร่วมกัน")]
    [SerializeField] private Transform havocDestination;

    [Tooltip("ดีเลย์สั้นๆ หลังเด้งครบครั้งสุดท้ายก่อนวาร์ป (วินาที)")]
    [SerializeField] private float delayBeforeTeleport = 0.15f;

    [Tooltip("ให้ลงจอดสู่พื้นทันทีหลังวาร์ปถึงเป้าหมาย")]
    [SerializeField] private bool landAfterTeleport = true;

    [Tooltip("ให้กดสวิตช์ FloorSwitch ที่อยู่ ณ จุดเป้าหมายโดยอัตโนมัติเมื่อวาร์ปถึง")]
    [SerializeField] private bool triggerSwitchOnTeleport = true;

    public int HavocBouncesBeforeTeleport
    {
        get => havocBouncesBeforeTeleport;
        set => havocBouncesBeforeTeleport = value;
    }
    #endregion

    #region Strength Potion Settings
    [Header("Strength Potion: Patrol & Push Box (ยาส้ม: บินลาดตระเวน & ดันกล่อง)")]
    [Tooltip("ความสูงที่ตัวลอยขึ้นขณะบินลาดตระเวน (หน่วย Unity ปรับได้อิสระ)")]
    [SerializeField] private float patrolLiftHeight = 0.5f;

    [Tooltip("ความเร็วในการบินลาดตระเวน ซ้าย-ขวา")]
    [SerializeField] private float patrolSpeed = 3.5f;

    [Tooltip("ความเร็วตอนดันกล่อง")]
    [SerializeField] private float patrolPushSpeed = 3f;

    [Tooltip("ระยะดันกล่องทีละครั้ง (0 = ดันรวดเดียวจนสุดทางชนกำแพง, >0 = ค่อยๆ ดันทีละระยะนี้)")]
    [SerializeField] private float patrolPushStep = 1f;

    [Tooltip("ความเร็วตอนบินกลับไปยังจุดเริ่มต้น หลังผลักกล่องเสร็จ")]
    [SerializeField] private float returnSpeed = 4f;

    [Tooltip("ทิศทางเริ่มต้นที่เริ่มบินลาดตระเวน (ซ้าย หรือ ขวา)")]
    [SerializeField] private AutoDirection patrolStartDirection = AutoDirection.Right;

    [Tooltip("Layer สิ่งกีดขวาง/กำแพง ที่ชนแล้วจะกลับทิศ (ถ้าเว้นว่างจะตรวจจับทุกอย่างที่ไม่ใช่ flyOverLayers)")]
    [SerializeField] private LayerMask patrolObstacleLayers;

    [Tooltip("Layer ของกล่องที่ดันได้ (ถ้าเว้นว่างจะตรวจหากล่องที่มี PushableBox อัตโนมัติ)")]
    [SerializeField] private LayerMask boxLayer;

    [Tooltip("(ไม่บังคับ) สไปรต์เฉพาะเมื่อดื่มยาส้ม (Patrol / Push) ถ้าเว้นว่างจะใช้ poweredUpSprite ปกติ")]
    [SerializeField] private Sprite strengthFormSprite;

    public float PatrolLiftHeight
    {
        get => patrolLiftHeight;
        set => patrolLiftHeight = value;
    }
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
            case FlyMode.Havoc: yield return HavocBounceRoutine(); break;
            case FlyMode.Ram: yield return RamRoutine(ToVector(flyDirection)); break;
            case FlyMode.PatrolPush: yield return PatrolPushRoutine(); break;
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

        // โหมดบินลาดตระเวนผลักกล่อง (ยาส้ม)
        if (isFlying && isPatrolPushing)
        {
            HandlePatrolCollision(collision);
            return;
        }

        // โหมด Havoc เด้งก่อนวาร์ป
        if (isFlying && isHavocBouncing)
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
                OnHavocBounceHit(normal);
            }
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
            return;
        }

        if (isFlying && isPatrolPushing)
        {
            HandlePatrolCollision(collision);
            return;
        }

        if (isFlying && isHavocBouncing)
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
                OnHavocBounceHit(normal);
            }
            return;
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

    private enum PatrolPushStage
    {
        None,
        Ascending,
        Patrolling,
        PushingBox,
        Returning,
        Descending
    }

    private FlyMode mode = FlyMode.Normal;
    private PotionType lastDrunkPotion = PotionType.None;
    private bool isFlying = false;
    private bool isBouncing = false;
    private bool isPatrolPushing = false;
    private bool isHavocBouncing = false;
    private int havocBounceCount = 0;
    private Coroutine activeHavocRoutine = null;
    private PatrolPushStage patrolPushStage = PatrolPushStage.None;
    private Vector2 patrolOriginPos;
    private Vector2 patrolDir = Vector2.right;
    private PushableBox patrolDetectedBox = null;
    private bool patrolHitWall = false;
    private Vector2 patrolWallNormal = Vector2.zero;
    private Coroutine activePatrolRoutine = null;
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
    public override bool CanBreakObstacles => IsRamming;

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
        return potion == PotionType.Light || potion == PotionType.Strength || IsComboPotion(potion);
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
                mode = FlyMode.Dizzy;
                break;
            case PotionType.Havoc:
                mode = FlyMode.Havoc;
                break;
            case PotionType.Surge:
                mode = FlyMode.Ram;
                break;
            case PotionType.Strength:
                mode = FlyMode.PatrolPush;
                break;
            default:
                mode = FlyMode.Normal;
                break;
        }
        Debug.Log($"[{animalName}] โหมดบิน: {mode} (ยา: {potion})");
    }

    protected override Sprite GetCustomPoweredUpSprite(PotionType potion)
    {
        if (potion == PotionType.Strength && strengthFormSprite != null)
            return strengthFormSprite;
        return base.GetCustomPoweredUpSprite(potion);
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
        if (activePatrolRoutine != null)
        {
            StopCoroutine(activePatrolRoutine);
            activePatrolRoutine = null;
        }
        if (activeHavocRoutine != null)
        {
            StopCoroutine(activeHavocRoutine);
            activeHavocRoutine = null;
        }
    }

    protected override void Update()
    {
        base.Update();

        if (isFlying && !isPatrolPushing && !isHavocBouncing && Time.time >= flyEndTime) Land();

        // ยกสไปรต์ขึ้น/ลงแบบนุ่มๆ
        if (spriteTransform != null)
        {
            Vector3 target = spriteBaseLocalPos + (isFlying ? Vector3.up * liftHeight : Vector3.zero);
            spriteTransform.localPosition = Vector3.Lerp(spriteTransform.localPosition, target, liftSmoothSpeed * Time.deltaTime);
        }

        // หันหน้าซ้าย-ขวาตามทิศทางการเด้ง
        if ((isBouncing || isHavocBouncing) && spriteRenderer != null && Mathf.Abs(FacingDirection.x) > 0.01f)
        {
            FaceHorizontal(FacingDirection.x < 0f);
        }
    }

    protected override void FixedUpdate()
    {
        if (isPatrolPushing)
        {
            // โหมดลาดตระเวนผลักกล่องจัดการการเคลื่อนที่และฟิสิกส์เองใน PatrolPushRoutine
            return;
        }

        if (isHavocBouncing)
        {
            float speed = moveSpeed * SpeedMultiplier;
            CircleCollider2D circle = bodyCollider as CircleCollider2D;
            float radius = (circle != null) ? circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * 0.95f : 0.45f;
            float step = speed * Time.fixedDeltaTime;
            float castDist = step + 0.06f;

            RaycastHit2D hit = Physics2D.CircleCast(rb.position, radius, currentBounceDir, castDist, ~flyOverLayers);
            if (hit.collider != null && !hit.collider.isTrigger && hit.collider.gameObject != gameObject)
            {
                OnHavocBounceHit(hit.normal);
            }
            else
            {
                rb.linearVelocity = currentBounceDir * speed + ConveyorVelocity;
            }
            return;
        }

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
        if (mode == FlyMode.PatrolPush)
        {
            if (activePatrolRoutine != null)
            {
                StopCoroutine(activePatrolRoutine);
                activePatrolRoutine = null;
            }

            activePatrolRoutine = StartCoroutine(PatrolPushRoutine());
            return true;
        }
        if (mode == FlyMode.Havoc)
        {
            if (activeHavocRoutine != null)
            {
                StopCoroutine(activeHavocRoutine);
                activeHavocRoutine = null;
            }

            activeHavocRoutine = StartCoroutine(HavocBounceRoutine());
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
        isPatrolPushing = false;
        isHavocBouncing = false;
        patrolPushStage = PatrolPushStage.None;
        currentBounceSpeedBonus = 0f;
        if (activeBounceRoutine != null)
        {
            StopCoroutine(activeBounceRoutine);
            activeBounceRoutine = null;
        }
        if (activePatrolRoutine != null)
        {
            StopCoroutine(activePatrolRoutine);
            activePatrolRoutine = null;
        }
        if (activeHavocRoutine != null)
        {
            StopCoroutine(activeHavocRoutine);
            activeHavocRoutine = null;
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

    #region Strength Potion (Patrol & Push Box)
    /// <summary>
    /// ทำงานเมื่อได้ยาส้ม (Strength Potion):
    /// 1. ลอยตัวขึ้นตามระดับ patrolLiftHeight
    /// 2. บินลาดตระเวน ซ้าย-ขวา ไปเรื่อยๆ เมื่อชนกำแพงจะสลับทิศทาง
    /// 3. เมื่อเจอกล่อง (PushableBox) จะดันกล่องไปข้างหน้าจนกระทั่งกล่องติดกำแพง/ดันต่อไม่ได้
    /// 4. เมื่อกล่องติดแล้ว บินกลับไปยังตำแหน่งตั้งต้นเดิม (originPos) แล้วลงจอด
    /// </summary>
    private IEnumerator PatrolPushRoutine()
    {
        isPatrolPushing = true;
        patrolPushStage = PatrolPushStage.Ascending;
        TakeOff();
        flyEndTime = float.MaxValue;
        StopMoving();

        patrolOriginPos = rb.position;
        float targetFlightY = patrolOriginPos.y + patrolLiftHeight;

        // ตรวจสอบเพดานด้านบนก่อนลอยตัว กันชนทะลุเพดาน
        LayerMask wallMask = patrolObstacleLayers.value != 0 ? patrolObstacleLayers : (LayerMask)(~flyOverLayers);
        RaycastHit2D ceilCheck = Physics2D.Raycast(patrolOriginPos, Vector2.up, patrolLiftHeight + 0.2f, wallMask);
        if (ceilCheck.collider != null && !ceilCheck.collider.isTrigger && ceilCheck.collider.gameObject != gameObject)
        {
            targetFlightY = Mathf.Min(targetFlightY, ceilCheck.point.y - 0.35f);
        }

        // --- 1. ลอยตัวขึ้นสู่ระดับความสูงบิน (Ascend) ---
        float ascendSpeed = Mathf.Max(2f, patrolSpeed);
        while (Mathf.Abs(rb.position.y - targetFlightY) > 0.02f)
        {
            float newY = Mathf.MoveTowards(rb.position.y, targetFlightY, ascendSpeed * Time.fixedDeltaTime);
            rb.position = new Vector2(patrolOriginPos.x, newY);
            yield return new WaitForFixedUpdate();
        }
        rb.position = new Vector2(patrolOriginPos.x, targetFlightY);

        // กำหนดทิศทางเริ่มต้น
        patrolDir = (patrolStartDirection == AutoDirection.Left) ? Vector2.left : Vector2.right;
        FacingDirection = patrolDir;
        FaceHorizontal(patrolDir.x < 0f);

        patrolDetectedBox = null;
        patrolHitWall = false;
        patrolWallNormal = Vector2.zero;
        float wallCooldown = 0f;
        patrolPushStage = PatrolPushStage.Patrolling;

        Debug.Log($"[{animalName}] เริ่มบินลาดตระเวน ทิศทาง {patrolDir} ความสูงลอยตัว {patrolLiftHeight}");

        // --- 2. บินลาดตระเวน ซ้าย-ขวา ไปเรื่อยๆ จนเจอกล่องหรือชนกำแพงสลับทิศ ---
        while (patrolDetectedBox == null)
        {
            wallCooldown -= Time.fixedDeltaTime;

            // เคลื่อนที่แนวนอนด้วยความเร็วลาดตระเวน
            float speed = patrolSpeed * SpeedMultiplier;
            rb.linearVelocity = new Vector2(patrolDir.x * speed, 0f) + ConveyorVelocity;

            // คุมระดับความสูงบินให้คงที่สม่ำเสมอ
            if (Mathf.Abs(rb.position.y - targetFlightY) > 0.03f)
            {
                float correctedY = Mathf.MoveTowards(rb.position.y, targetFlightY, 3f * Time.fixedDeltaTime);
                rb.position = new Vector2(rb.position.x, correctedY);
            }

            // CircleCast ตรวจจับสิ่งกีดขวาง/กล่องล่วงหน้า
            CircleCollider2D circle = bodyCollider as CircleCollider2D;
            float radius = (circle != null) ? circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * 0.9f : 0.45f;
            float checkDist = Mathf.Max(0.12f, speed * Time.fixedDeltaTime + 0.06f);
            LayerMask checkMask = patrolObstacleLayers.value != 0 ? (patrolObstacleLayers | boxLayer) : (LayerMask)(~flyOverLayers);

            RaycastHit2D hit = Physics2D.CircleCast(rb.position, radius, patrolDir, checkDist, checkMask);
            if (hit.collider != null && !hit.collider.isTrigger && hit.collider.gameObject != gameObject)
            {
                PushableBox box = hit.collider.GetComponentInParent<PushableBox>();
                if (box != null && !box.IsBroken)
                {
                    patrolDetectedBox = box;
                    break;
                }
                else if (wallCooldown <= 0f)
                {
                    if (hit.normal.sqrMagnitude < 0.01f || Vector2.Dot(patrolDir, hit.normal) < -0.2f)
                    {
                        patrolHitWall = true;
                        patrolWallNormal = hit.normal;
                    }
                }
            }

            // ถ้าชนกำแพง -> สลับซ้าย-ขวา (Reverse Direction)
            if (patrolHitWall && wallCooldown <= 0f)
            {
                patrolDir = -patrolDir;
                FacingDirection = patrolDir;
                FaceHorizontal(patrolDir.x < 0f);

                Vector2 nudge = (patrolWallNormal.sqrMagnitude > 0.01f) ? patrolWallNormal.normalized : -patrolDir;
                rb.position += nudge * 0.08f;

                wallCooldown = 0.3f;
                patrolHitWall = false;
                patrolWallNormal = Vector2.zero;
                Debug.Log($"[{animalName}] บินชนกำแพง -> สลับทิศทางเป็น {patrolDir}");
            }

            yield return new WaitForFixedUpdate();
        }

        // --- 3. ดันกล่องไปข้างหน้าจนกระทั่งกล่องติด (Push Box Until Stuck) ---
        patrolPushStage = PatrolPushStage.PushingBox;
        PushableBox targetBox = patrolDetectedBox;
        StopMoving();
        rb.linearVelocity = Vector2.zero;
        Debug.Log($"[{animalName}] เจอกล่อง {targetBox.name} เริ่มดันกล่องไปทาง {patrolDir}!");

        Collider2D targetBoxCol = targetBox.GetComponent<Collider2D>();
        float boxExtentX = (targetBoxCol != null) ? targetBoxCol.bounds.extents.x : 0.5f;
        CircleCollider2D catCircle = bodyCollider as CircleCollider2D;
        float catExtentX = (catCircle != null) ? catCircle.radius * transform.lossyScale.x : 0.45f;
        float followOffset = boxExtentX + catExtentX + 0.05f;

        bool boxStuck = false;
        while (!boxStuck && targetBox != null && !targetBox.IsBroken)
        {
            float pushDist = patrolPushStep > 0f ? patrolPushStep : 50f;
            bool pushed = targetBox.TryPush(patrolDir, pushDist, patrolPushSpeed, this);

            if (!pushed)
            {
                // ดันต่อไม่ได้แล้ว = กล่องติดกำแพง/สิ่งกีดขวางแล้ว
                Debug.Log($"[{animalName}] ดันกล่อง {targetBox.name} จนติดกำแพงแล้ว!");
                boxStuck = true;
                break;
            }

            // บินตามหลังกล่องไปเรื่อยๆ จนกว่ากล่องจะหยุดเลื่อนในจังหวะนั้น
            while (targetBox != null && targetBox.IsMoving)
            {
                Vector2 targetCatPos = new Vector2(targetBox.transform.position.x - patrolDir.x * followOffset, targetFlightY);
                float step = patrolPushSpeed * Time.fixedDeltaTime;
                Vector2 nextCatPos = Vector2.MoveTowards(rb.position, targetCatPos, step * 1.35f);
                rb.MovePosition(nextCatPos);
                yield return new WaitForFixedUpdate();
            }

            if (patrolPushStep > 0f && !boxStuck)
            {
                yield return new WaitForSeconds(0.08f);
            }
        }

        // --- 4. บินกลับไปยังตำแหน่งตั้งต้นเดิม (Return To Origin) ---
        patrolPushStage = PatrolPushStage.Returning;
        Debug.Log($"[{animalName}] ดันเสร็จแล้ว กำลังบินกลับไปยังตำแหน่งตั้งต้น {patrolOriginPos}");
        StopMoving();
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(0.2f);

        Vector2 returnHorizontalDir = (patrolOriginPos.x < rb.position.x) ? Vector2.left : Vector2.right;
        FacingDirection = returnHorizontalDir;
        FaceHorizontal(returnHorizontalDir.x < 0f);

        float returnSafetyTimer = 0f;
        while (Mathf.Abs(rb.position.x - patrolOriginPos.x) > 0.05f && returnSafetyTimer < 10f)
        {
            returnSafetyTimer += Time.fixedDeltaTime;
            float step = returnSpeed * Time.fixedDeltaTime;
            Vector2 nextH = Vector2.MoveTowards(rb.position, new Vector2(patrolOriginPos.x, targetFlightY), step);
            rb.MovePosition(nextH);
            yield return new WaitForFixedUpdate();
        }
        rb.MovePosition(new Vector2(patrolOriginPos.x, targetFlightY));

        // --- 5. ลดระดับลงสู่ตำแหน่งตั้งต้นและลงจอด (Descend & Land) ---
        patrolPushStage = PatrolPushStage.Descending;
        returnSafetyTimer = 0f;
        while (Mathf.Abs(rb.position.y - patrolOriginPos.y) > 0.03f && returnSafetyTimer < 5f)
        {
            returnSafetyTimer += Time.fixedDeltaTime;
            float step = returnSpeed * Time.fixedDeltaTime;
            Vector2 nextPos = Vector2.MoveTowards(rb.position, patrolOriginPos, step);
            rb.MovePosition(nextPos);
            yield return new WaitForFixedUpdate();
        }
        rb.MovePosition(patrolOriginPos);
        rb.linearVelocity = Vector2.zero;

        patrolPushStage = PatrolPushStage.None;
        isPatrolPushing = false;
        activePatrolRoutine = null;
        Land();
        Debug.Log($"[{animalName}] บินกลับถึงตำแหน่งตั้งต้นและลงจอดเรียบร้อยแล้ว");
    }

    /// <summary>
    /// จัดการการชนของโหมดบินลาดตระเวน (ตรวจหากล่องเพื่อผลัก หรือตรวจหากำแพงเพื่อสลับทิศทาง)
    /// </summary>
    private void HandlePatrolCollision(Collision2D collision)
    {
        if (patrolPushStage != PatrolPushStage.Patrolling) return;

        PushableBox box = collision.gameObject.GetComponentInParent<PushableBox>();
        if (box != null && !box.IsBroken)
        {
            patrolDetectedBox = box;
            return;
        }

        Vector2 normal = Vector2.zero;
        int count = collision.contactCount;
        for (int i = 0; i < count; i++)
        {
            normal += collision.contacts[i].normal;
        }

        if (normal.sqrMagnitude > 0.01f)
        {
            normal.Normalize();
            if (Vector2.Dot(patrolDir, normal) < -0.2f)
            {
                patrolHitWall = true;
                patrolWallNormal = normal;
            }
        }
        else
        {
            patrolHitWall = true;
            patrolWallNormal = -patrolDir;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // วาดระดับความสูงที่บินลอยขึ้นตอนกินยาส้ม (Patrol Lift Height)
        Vector3 basePos = Application.isPlaying ? (Vector3)patrolOriginPos : transform.position;
        Vector3 flightPos = basePos + Vector3.up * patrolLiftHeight;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.85f); // สีส้ม
        Gizmos.DrawLine(basePos, flightPos);
        Gizmos.DrawWireSphere(flightPos, 0.2f);
        Gizmos.DrawLine(flightPos + Vector3.left * 1.5f, flightPos + Vector3.right * 1.5f);

        // วาดเส้นและจุดวาร์ปของ Havoc
        Transform dest = GetHavocDestination();
        if (dest != null)
        {
            Gizmos.color = new Color(0.7f, 0.2f, 1f, 0.9f); // สีม่วง Havoc
            Gizmos.DrawLine(transform.position, dest.position);
            Gizmos.DrawWireSphere(dest.position, 0.35f);
        }
    }
#endif
    #endregion

    #region Havoc API
    /// <summary>
    /// หาจุดหมายปลายทางสำหรับวาร์ปของ Havoc (เรียงลำดับ: havocDestination -> targetDestination -> skywardDestination -> Teleport point)
    /// </summary>
    private Transform GetHavocDestination()
    {
        if (havocDestination != null) return havocDestination;
        if (targetDestination != null) return targetDestination;
        if (skywardDestination != null) return skywardDestination;

        Transform child = transform.Find("Teleport point");
        if (child != null) return child;

        GameObject tpObj = GameObject.Find("Teleport point");
        if (tpObj != null) return tpObj.transform;

        return null;
    }

    /// <summary>
    /// ฟังก์ชันสะท้อนออกจากกำแพงของโหมด Havoc เพิ่มความเร็วและนับจำนวนครั้งที่เด้งจนครบ havocBouncesBeforeTeleport
    /// </summary>
    private void OnHavocBounceHit(Vector2 normal)
    {
        if (!isHavocBouncing || normal.sqrMagnitude < 0.0001f) return;
        normal.Normalize();

        if (Vector2.Dot(currentBounceDir, normal) < -0.05f)
        {
            currentBounceDir = Vector2.Reflect(currentBounceDir, normal).normalized;
            FacingDirection = currentBounceDir;
            FaceHorizontal(currentBounceDir.x < 0f);

            rb.position += normal * 0.06f;

            OnBounce();
            havocBounceCount++;
            Debug.Log($"[{animalName}] Havoc เด้งครั้งที่ {havocBounceCount}/{havocBouncesBeforeTeleport}");

            float speed = moveSpeed * SpeedMultiplier;
            rb.linearVelocity = currentBounceDir * speed + ConveyorVelocity;
        }
    }

    /// <summary>
    /// โหมด Havoc: เด้งสะท้อนกำแพง 5 ครั้ง (หรือตามที่ตั้งไว้ใน havocBouncesBeforeTeleport) แล้ววาร์ปไปยังจุดหมายเป้าหมาย
    /// </summary>
    private IEnumerator HavocBounceRoutine()
    {
        isHavocBouncing = true;
        isBouncing = true;
        havocBounceCount = 0;
        currentBounceSpeedBonus = 0f;
        TakeOff();
        flyEndTime = float.MaxValue;
        StopMoving();

        Transform dest = GetHavocDestination();
        Vector2 toDest = (dest != null) ? ((Vector2)dest.position - rb.position).normalized : ToVector(flyDirection);
        if (toDest.sqrMagnitude < 0.0001f) toDest = Vector2.right;

        // สุ่มมุมกระจายตัวออกไปเล็กน้อยเพื่อให้เด้งชิ่งกำแพง
        float baseAngle = Mathf.Atan2(toDest.y, toDest.x) * Mathf.Rad2Deg;
        float randomOffset = Random.Range(-55f, 55f);
        if (Mathf.Abs(randomOffset) < 15f) randomOffset = Mathf.Sign(randomOffset != 0 ? randomOffset : 1f) * 25f;

        float launchAngle = (baseAngle + randomOffset) * Mathf.Deg2Rad;
        currentBounceDir = new Vector2(Mathf.Cos(launchAngle), Mathf.Sin(launchAngle)).normalized;
        FacingDirection = currentBounceDir;
        FaceHorizontal(currentBounceDir.x < 0f);

        float speed = moveSpeed * SpeedMultiplier;
        rb.linearVelocity = currentBounceDir * speed + ConveyorVelocity;

        Debug.Log($"[{animalName}] เริ่ม Havoc: ทิศทาง {currentBounceDir}, กำหนดเด้ง {havocBouncesBeforeTeleport} ครั้งก่อนวาร์ป");

        // 1. เด้งสะท้อนกำแพงจนครบตามจำนวนที่กำหนด (ค่าเริ่มต้น 5 ครั้ง)
        while (isHavocBouncing && havocBounceCount < havocBouncesBeforeTeleport)
        {
            yield return null;
        }

        // เด้งครบแล้ว หยุดการเคลื่อนที่
        isHavocBouncing = false;
        rb.linearVelocity = Vector2.zero;

        // 2. หน่วงเวลาสั้นๆ ให้เห็นการชนกำแพงครั้งสุดท้าย
        if (delayBeforeTeleport > 0f)
        {
            yield return new WaitForSeconds(delayBeforeTeleport);
        }

        // 3. วาร์ป (Teleport) ไปยังจุดหมายเป้าหมายทันที
        dest = GetHavocDestination();
        if (dest != null)
        {
            Debug.Log($"[{animalName}] Havoc เด้งครบ {havocBouncesBeforeTeleport} ครั้งแล้ว! วาร์ปไปยัง {dest.name} ({dest.position})");
            TeleportTo(dest.position);

            // 4. สับสวิตช์ FloorSwitch ที่อยู่ ณ จุดวาร์ป (ถ้ามี)
            if (triggerSwitchOnTeleport)
            {
                FloorSwitch sw = dest.GetComponentInParent<FloorSwitch>();
                if (sw == null) sw = dest.GetComponentInChildren<FloorSwitch>();
                if (sw == null)
                {
                    Collider2D col = Physics2D.OverlapCircle(dest.position, 0.6f);
                    if (col != null) sw = col.GetComponentInParent<FloorSwitch>();
                }
                if (sw != null)
                {
                    sw.RemoteToggle();
                    Debug.Log($"[{animalName}] Havoc วาร์ปถึงสวิตช์ -> สับสวิตช์ {sw.name} สำเร็จ!");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[{animalName}] Havoc เด้งครบ {havocBouncesBeforeTeleport} ครั้งแล้ว แต่ไม่พบจุดเป้าหมายสำหรับวาร์ป!");
        }

        // 5. ลงจอด
        if (landAfterTeleport)
        {
            Land();
        }

        isBouncing = false;
        activeHavocRoutine = null;
    }
    #endregion
}