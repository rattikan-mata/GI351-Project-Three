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

    [Header("Audio (เสียงเฉพาะของวัวบิน)")]
    [Tooltip("เสียงตอนบินขึ้น / ปีกกระพือ")]
    [SerializeField] private AudioClip flySound;

    [Tooltip("เสียงตอนบินเด้งชนกำแพง")]
    [SerializeField] private AudioClip bounceSound;

    [Tooltip("เสียงตอนพุ่งชนความเร็วสูง (Surge / Ramming)")]
    [SerializeField] private AudioClip ramSound;

    [Tooltip("เสียงตอนวาร์ป (Teleport / TP ในโหมด Havoc)")]
    [SerializeField] private AudioClip teleportSound;

    [Range(0f, 1f)]
    [Tooltip("ระดับความดังของเสียงวาร์ป (0 - 1)")]
    [SerializeField] private float teleportVolume = 1f;
    #endregion

    #region Combo Settings
    [Header("Skyward (เขียว+ฟ้า): จุดรีเซ็ท & จุดเป้าหมาย")]
    [Tooltip("จุดเป้าหมายเฉพาะสำหรับ Skyward (แยกอิสระจาก Havoc ไม่ใช้ร่วมกัน)")]
    [SerializeField] private Transform skywardDestination;

    [Tooltip("จุดรีเซ็ทกลางทาง 1: เมื่อสุ่มเด้งเกิน (2-3 ครั้ง) แล้วยังไม่ถึงเป้าหมาย จะเด้งกลับมาเริ่มที่จุดนี้")]
    [SerializeField] private Transform midwayResetPoint;

    [FormerlySerializedAs("teleportTarget")]
    [Tooltip("จุดเป้าหมายทั่วไป (สำหรับ Skyward)")]
    [SerializeField] private Transform targetDestination;

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
    #endregion

    #region Skyward Door Pierce
    [Header("Skyward Door Pierce (การทะลุประตูหลังแตะสวิตช์)")]
    [Tooltip("เมื่อตัววัวแตะปุ่ม/สวิตช์ในโหมด Skyward แล้ว จะได้รับความสามารถทะลุประตูได้")]
    [SerializeField] private bool skywardPierceDoorsAfterSwitch = true;

    [Tooltip("เมื่อตัววัวบินไปชน/แตะ FloorSwitch ในโหมด Skyward ให้กดสวิตช์ทำงานทันที")]
    [SerializeField] private bool skywardTriggerSwitchOnTouch = true;

    [Tooltip("หากประตูเป็นแบบพังได้ (Breakable) เมื่อทะลุผ่านจะทำให้ประตูพังด้วยหรือไม่")]
    [SerializeField] private bool skywardBreakDoorsOnPierce = false;

    public bool SkywardPierceDoorsAfterSwitch
    {
        get => skywardPierceDoorsAfterSwitch;
        set => skywardPierceDoorsAfterSwitch = value;
    }

    public bool SkywardBreakDoorsOnPierce
    {
        get => skywardBreakDoorsOnPierce;
        set => skywardBreakDoorsOnPierce = value;
    }

    public bool SkywardSwitchTouched
    {
        get => skywardSwitchTouched;
        set
        {
            skywardSwitchTouched = value;
            if (skywardSwitchTouched && skywardPierceDoorsAfterSwitch)
            {
                IgnoreAllDoors();
            }
        }
    }
    #endregion

    #region Surge Settings
    [Header("Surge (ฟ้า+ส้ม): บินลาดตระเวน พังกล่องพังประตู & บินกลับที่เดิม")]
    [Tooltip("ความสูงที่ตัวลอยขึ้นขณะบิน Surge (หน่วย Unity ปรับได้อิสระ)")]
    [SerializeField] private float surgeLiftHeight = 0.5f;

    [Tooltip("ความเร็วในการบินพุ่งทำลายสิ่งกีดขวาง (Surge Speed)")]
    [SerializeField] private float surgeSpeed = 4.5f;

    [Tooltip("ความเร็วตอนบินกลับไปยังตำแหน่งต้นฉบับหลังพังสิ่งกีดขวางเสร็จ")]
    [SerializeField] private float surgeReturnSpeed = 4f;

    [Tooltip("ทิศทางเริ่มต้นที่เริ่มบิน (ซ้าย หรือ ขวา)")]
    [SerializeField] private AutoDirection surgeStartDirection = AutoDirection.Right;

    [Tooltip("ระยะเวลาสูงสุดที่บินลาดตระเวนในกรณีที่ไม่มีกล่อง/ประตู (วินาที, 0 = บินไปมาซ้าย-ขวาตลอดจนกว่าจะเจอกล่อง หรือผู้เล่นสั่งหยุด)")]
    [SerializeField] private float surgeMaxPatrolDurationWithoutTarget = 0f;
    [SerializeField, HideInInspector] private int surgeMaxWallReverses = 0;

    [Tooltip("จำนวนครั้งที่ต้องพุ่งชนกล่องถึงจะแตก (ปรับได้ที่ตัวสัตว์ ค่าเริ่มต้น 3 ครั้ง)")]
    [SerializeField, Min(1)] private int surgeHitsToBreakBox = 3;

    [Tooltip("ระยะที่ตัวบินกระเด็นถอยหลังหลังชนกล่องแต่ละครั้ง (หน่วย Unity)")]
    [SerializeField] private float surgeRecoilDistance = 1.3f;

    [Tooltip("ความเร็วตอนกระเด็นถอยหลัง")]
    [SerializeField] private float surgeRecoilSpeed = 6f;

    [Tooltip("เวลาหยุดตั้งหลักกลางอากาศหลังกระเด็น ก่อนพุ่งกลับไปชนกล่องอีกรอบ (วินาที)")]
    [SerializeField] private float surgePauseBeforeRecharge = 0.25f;

    [Tooltip("ความเร็วตอนพุ่งกลับไปชนกล่องซ้ำ")]
    [SerializeField] private float surgeChargeSpeed = 8f;

    [Tooltip("พังกล่องทันทีเมื่อพุ่งชน (ถ้าเปิดไว้ จะพังกล่องทันทีใน 1 ฮิต ไม่กระเด็นถอย)")]
    [SerializeField] private bool surgeInstantBreakBox = false;

    [Tooltip("สามารถชนพังกล่องได้ (PushableBox)")]
    [SerializeField] private bool surgeBreakBoxes = true;

    [Tooltip("สามารถชนพังประตูได้ (Door)")]
    [SerializeField] private bool surgeBreakDoors = true;

    [Tooltip("Layer สิ่งกีดขวาง/กำแพงแข็ง ที่ชนแล้วสลับทิศหรือติด (ถ้าเว้นว่างจะตรวจจับทุกอย่างที่ไม่ใช่ flyOverLayers)")]
    [SerializeField] private LayerMask surgeObstacleLayers;

    [Tooltip("(ไม่บังคับ) สไปรต์เฉพาะเมื่อดื่มยา Surge ถ้าเว้นว่างจะใช้ poweredUpSprite ปกติ")]
    [SerializeField] private Sprite surgeFormSprite;

    [SerializeField, HideInInspector] private float ramSpeedMultiplier = 2.5f;
    [SerializeField, HideInInspector] private float ramDistance = 8f;
    [SerializeField, HideInInspector] private LayerMask ramCollideLayers;

    public float SurgeLiftHeight
    {
        get => surgeLiftHeight;
        set => surgeLiftHeight = value;
    }
    public float SurgeSpeed
    {
        get => surgeSpeed;
        set => surgeSpeed = value;
    }
    public int SurgeHitsToBreakBox
    {
        get => surgeHitsToBreakBox;
        set => surgeHitsToBreakBox = Mathf.Max(1, value);
    }
    #endregion

    #region Havoc Settings
    [Header("Havoc (เขียว+ส้ม): เด้งก่อนวาร์ป & เด้งต่อ")]
    [Tooltip("จุดเป้าหมายเฉพาะสำหรับ Havoc (แยกอิสระ 100% ไม่ใช้ร่วมกับ Skyward)")]
    [SerializeField] private Transform havocDestination;

    [Tooltip("จำนวนครั้งที่เด้งสะท้อนกำแพงก่อนวาร์ป (ค่าเริ่มต้น 5 ครั้ง)")]
    [SerializeField] private int havocBouncesBeforeTeleport = 5;

    [Tooltip("ดีเลย์สั้นๆ หลังเด้งครบครั้งสุดท้ายก่อนวาร์ป (วินาที)")]
    [SerializeField] private float delayBeforeTeleport = 0.15f;

    [Tooltip("หลังวาร์ปถึงจุดเป้าหมายแล้ว ให้เด้งสะท้อนกำแพงต่อหรือไม่ (ติ๊กไว้ = เด้งต่อ)")]
    [SerializeField] private bool bounceAfterTeleport = true;

    [Tooltip("จำนวนครั้งที่เด้งต่อหลังวาร์ป (ค่าเริ่มต้น 10 ครั้ง, 0 = เด้งตามเวลา)")]
    [SerializeField] private int havocPostTeleportBounces = 10;

    [Tooltip("ระยะเวลาสูงสุดที่เด้งต่อหลังวาร์ป (วินาที, 0 = เด้งตามจำนวนครั้งเท่านั้น)")]
    [SerializeField] private float havocPostTeleportBounceDuration = 8f;

    [Tooltip("ให้กดสวิตช์ FloorSwitch ที่อยู่ ณ จุดเป้าหมายโดยอัตโนมัติเมื่อวาร์ปถึง")]
    [SerializeField] private bool triggerSwitchOnTeleport = true;

    [Tooltip("ให้ลงจอดสู่พื้นเมื่อสิ้นสุดการเด้งต่อหลังวาร์ป")]
    [SerializeField] private bool landAfterPostTeleport = true;

    public int HavocBouncesBeforeTeleport
    {
        get => havocBouncesBeforeTeleport;
        set => havocBouncesBeforeTeleport = value;
    }
    public Transform HavocDestination
    {
        get => havocDestination;
        set => havocDestination = value;
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

    [Header("Switch Landing Delay (การหน่วงเวลาหลังแตะสวิตช์)")]
    [Tooltip("ติ๊กไว้ = เมื่อบินไปแตะ/ชนสวิตช์ (FloorSwitch) จะยังลอยค้างอยู่กลางอากาศตรงสวิตช์ก่อน แล้วค่อยร่วงลงสู่พื้น")]
    [SerializeField] private bool hoverOnSwitchTouch = true;

    [Tooltip("เวลาที่บินค้างอยู่กลางอากาศหลังแตะสวิตช์ ก่อนจะร่วงลงสู่พื้น (วินาที, 0 = ร่วงทันที)")]
    [SerializeField] private float delayAfterSwitchBeforeLand = 2f;

    [Tooltip("ติ๊กไว้ = สามารถกดสวิตช์ได้ทันทีที่บินไปแตะ แม้สวิตช์นั้นจะตั้ง ignoreAirborneAnimals ไว้ก็ตาม")]
    [SerializeField] private bool triggerAnySwitchOnTouch = true;

    [Header("Door Flight (การบินผ่านประตู - ยาฟ้า)")]
    [Tooltip("ติ๊กไว้ = เมื่อบินไปชนประตูที่ปิดอยู่ จะลอยค้างรอที่หน้าประตู (ไม่หยุดบิน/ไม่ตกพื้น) จนกว่าประตูจะเปิด แล้วบินต่อ")]
    [SerializeField] private bool hoverAtClosedDoor = true;

    [Tooltip("เวลาสูงสุดที่จะลอยรอหน้าประตูที่ปิดอยู่ (วินาที, 0 = รอไปเรื่อยๆ จนกว่าประตูจะเปิด)")]
    [SerializeField] private float maxDoorWaitDuration = 0f;

    [Tooltip("ติ๊กไว้ = ไม่ชนประตูที่เปิดอยู่เลย (บินผ่านประตูที่เปิดอยู่ได้ทันทีอย่างราบรื่น ไม่ติดขอบประตู)")]
    [SerializeField] private bool passOpenDoors = true;

    [Tooltip("ติ๊กไว้ = ตัวบิน (ยาฟ้า) สามารถบินทะลุประตูได้ทุกบาน")]
    [SerializeField] private bool flyThroughAnyDoor = false;

    protected override IEnumerator AutoRoutine()
    {
        switch (mode)
        {
            case FlyMode.Zigzag:
            case FlyMode.Dizzy: yield return BounceRoutine(dizzyBounces, true); break;
            case FlyMode.Havoc: yield return HavocBounceRoutine(); break;
            case FlyMode.Ram: yield return SurgePatrolSmashRoutine(); break;
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
        Vector2 target = rb.position + dir * flyDistance;
        MoveTo(target);

        while (isFlying)
        {
            // ถ้าระหว่างทางมีการแตะสวิตช์และกำลังลอยค้างอยู่ ให้รอจนกว่าจะครบเวลาหน่วง
            if (isSwitchHovering)
            {
                while (isSwitchHovering) yield return null;
                break; // แตะสวิตช์และลอยค้างจนครบเวลาแล้ว ลงพื้น
            }

            // ถ้าระหว่างทางชนประตูที่ปิดอยู่และกำลังลอยรอประตูเปิด
            if (isDoorHovering)
            {
                while (isDoorHovering) yield return null;

                // ประตูเปิดแล้ว! ถ้ายังบินอยู่ ให้บินต่อไปยังเป้าหมายเดิม
                if (isFlying && !isSwitchHovering)
                {
                    MoveTo(target);
                }
            }

            // ถ้าถึงจุดหมายแล้ว หรือติดสิ่งกีดขวางอื่นๆ (ที่ไม่ใช่ประตู/สวิตช์)
            if (!IsMoving && !isDoorHovering && !isSwitchHovering)
            {
                break;
            }

            yield return null;
        }

        // ถึงแล้ว (หรือชนสิ่งกีดขวาง หรือแตะสวิตช์ครบเวลาแล้ว) ลงพื้น
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

        for (int leg = 0; leg < zigzagMaxLegs && traveled < flyDistance && !isSwitchHovering; leg++)
        {
            Vector2 start = rb.position;
            zigzagWallHit = false;
            MoveTo(start + dir * zigzagStep + perp * side * zigzagAmplitude);

            while (IsMoving && !zigzagWallHit && !isSwitchHovering) yield return null;

            if (isSwitchHovering) break;

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

        while (isSwitchHovering) yield return null;

        if (isFlying) Land();
    }

    /// <summary>บินและเด้งสะท้อนกำแพง โดยมีจุดรีเซ็ทกลางทาง สุ่มทิศและสุ่มเด้ง 2-3 ครั้ง หากไม่ถึงเป้าหมายจะเด้งกลับมารีเซ็ท เมื่อถึงเป้าหมายจะปล่อยเด้งอิสระ</summary>
    private IEnumerator BounceRoutine(int bounceCount, bool allowTeleportAfter, Vector2? customDestination = null)
    {
        isBouncing = true;
        skywardSwitchTouched = false;
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

            PlayAnimalSound(bounceSound);

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

    private void OnTriggerEnter2D(Collider2D other)
    {
        FloorSwitch sw = other.GetComponentInParent<FloorSwitch>();
        if (sw == null) sw = other.GetComponentInChildren<FloorSwitch>();
        if (sw != null)
        {
            // ถ้าอยู่ในโหมด Skyward ให้สั่งเปิดสวิตช์และปลดล็อกการทะลุประตู
            if (isFlying && (isBouncing || lastDrunkPotion == PotionType.Skyward))
            {
                if (skywardTriggerSwitchOnTouch) sw.ForceToggle();
                if (skywardPierceDoorsAfterSwitch) OnSkywardSwitchTriggered();
            }

            if (isFlying && hoverOnSwitchTouch && !isSwitchHovering)
            {
                TryHandleSwitchHover(sw);
            }
        }

        if (isFlying)
        {
            Door door = other.GetComponentInParent<Door>();
            if (door == null) door = other.GetComponentInChildren<Door>();
            if (door != null)
            {
                if (flyThroughAnyDoor || door.IsOpen || door.IsBroken ||
                    (skywardSwitchTouched && skywardPierceDoorsAfterSwitch))
                {
                    IgnoreDoorCollision(door, true);
                    if (skywardBreakDoorsOnPierce && door.IsBreakable && !door.IsBroken)
                    {
                        door.BreakDoor();
                    }
                }
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isFlying)
        {
            Door door = other.GetComponentInParent<Door>();
            if (door == null) door = other.GetComponentInChildren<Door>();
            if (door != null)
            {
                if (flyThroughAnyDoor || door.IsOpen || door.IsBroken ||
                    (skywardSwitchTouched && skywardPierceDoorsAfterSwitch))
                {
                    IgnoreDoorCollision(door, true);
                    if (skywardBreakDoorsOnPierce && door.IsBreakable && !door.IsBroken)
                    {
                        door.BreakDoor();
                    }
                }
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // ตรวจจับสวิตช์เมื่อบินชน
        FloorSwitch sw = collision.gameObject.GetComponentInParent<FloorSwitch>();
        if (sw == null) sw = collision.gameObject.GetComponentInChildren<FloorSwitch>();
        if (sw != null)
        {
            // ถ้าอยู่ในโหมด Skyward ให้สั่งเปิดสวิตช์และปลดล็อกการทะลุประตู
            if (isFlying && (isBouncing || lastDrunkPotion == PotionType.Skyward))
            {
                if (skywardTriggerSwitchOnTouch) sw.ForceToggle();
                if (skywardPierceDoorsAfterSwitch) OnSkywardSwitchTriggered();
            }

            if (isFlying && hoverOnSwitchTouch && !isSwitchHovering)
            {
                TryHandleSwitchHover(sw);
                return;
            }
        }

        // ตรวจจับประตูเมื่อบินชน (ยาฟ้า: ไม่ตกพื้นทันทีเมื่อชนประตู / Skyward: ทะลุผ่านประตูหลังแตะสวิตช์)
        if (isFlying)
        {
            Door door = collision.gameObject.GetComponentInParent<Door>();
            if (door == null) door = collision.gameObject.GetComponentInChildren<Door>();
            if (door != null)
            {
                if (flyThroughAnyDoor || door.IsOpen || door.IsBroken ||
                    (skywardSwitchTouched && skywardPierceDoorsAfterSwitch))
                {
                    IgnoreDoorCollision(door, true);
                    if (skywardBreakDoorsOnPierce && door.IsBreakable && !door.IsBroken)
                    {
                        door.BreakDoor();
                    }
                    return;
                }

                if (mode == FlyMode.Normal && hoverAtClosedDoor && !isDoorHovering)
                {
                    TryHandleDoorHover(door);
                    return;
                }
            }
        }

        // โหมด Surge: บินลาดตระเวน พังกล่องพังประตู (ฟ้า+ส้ม)
        if (isFlying && isSurgePatrolling)
        {
            HandleSurgeCollision(collision);
            return;
        }

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
            Door door = collision.gameObject.GetComponentInParent<Door>();
            if (door == null) door = collision.gameObject.GetComponentInChildren<Door>();
            if (door != null && (flyThroughAnyDoor || door.IsOpen || door.IsBroken ||
                                 (skywardSwitchTouched && skywardPierceDoorsAfterSwitch)))
            {
                IgnoreDoorCollision(door, true);
                return;
            }

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

        // โหมด Havoc เด้งก่อนวาร์ป หรือเด้งต่อหลังวาร์ป
        if (isFlying && (isHavocBouncing || isHavocPostBouncing))
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
        if (isFlying)
        {
            Door door = collision.gameObject.GetComponentInParent<Door>();
            if (door == null) door = collision.gameObject.GetComponentInChildren<Door>();
            if (door != null)
            {
                if (flyThroughAnyDoor || door.IsOpen || door.IsBroken ||
                    (skywardSwitchTouched && skywardPierceDoorsAfterSwitch))
                {
                    IgnoreDoorCollision(door, true);
                    return;
                }

                if (mode == FlyMode.Normal && hoverAtClosedDoor && !isDoorHovering)
                {
                    TryHandleDoorHover(door);
                    return;
                }
            }
        }

        if (isFlying && isSurgePatrolling)
        {
            HandleSurgeCollision(collision);
            return;
        }

        if (isFlying && isBouncing && !IsRamming)
        {
            Door door = collision.gameObject.GetComponentInParent<Door>();
            if (door == null) door = collision.gameObject.GetComponentInChildren<Door>();
            if (door != null && (flyThroughAnyDoor || door.IsOpen || door.IsBroken ||
                                 (skywardSwitchTouched && skywardPierceDoorsAfterSwitch)))
            {
                IgnoreDoorCollision(door, true);
                return;
            }

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

        if (isFlying && (isHavocBouncing || isHavocPostBouncing))
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

    #region Switch Hover Methods
    private void TryHandleSwitchHover(FloorSwitch sw)
    {
        if (sw == null || isSwitchHovering || !isFlying) return;

        // ไม่รบกวนโหมดพิเศษที่จัดการฟิสิกส์การเคลื่อนที่เฉพาะ เช่น Surge / Havoc / PatrolPush
        if (isSurgePatrolling || isPatrolPushing || isBouncing || isHavocBouncing || isHavocPostBouncing) return;

        // ถ้าสวิตช์เปิด ignoreAirborneAnimals ไว้และเปิด triggerAnySwitchOnTouch ให้สลับสวิตช์
        if (triggerAnySwitchOnTouch && sw.IgnoreAirborneAnimals)
        {
            sw.ForceToggle();
        }

        if (switchHoverRoutine != null)
        {
            StopCoroutine(switchHoverRoutine);
            switchHoverRoutine = null;
        }

        switchHoverRoutine = StartCoroutine(SwitchHoverRoutine(sw));
    }

    private IEnumerator SwitchHoverRoutine(FloorSwitch sw)
    {
        isSwitchHovering = true;
        StopMoving();
        rb.linearVelocity = Vector2.zero;
        flyEndTime = float.MaxValue;

        Vector2 hoverPos = rb.position;

        if (delayAfterSwitchBeforeLand > 0f)
        {
            float timer = 0f;
            while (timer < delayAfterSwitchBeforeLand && isFlying)
            {
                timer += Time.deltaTime;
                rb.linearVelocity = Vector2.zero;
                rb.position = hoverPos;
                yield return null;
            }
        }

        isSwitchHovering = false;
        switchHoverRoutine = null;

        if (isFlying)
        {
            Land();
        }
    }
    #endregion

    #region Door Flight Methods
    private readonly System.Collections.Generic.HashSet<Collider2D> ignoredDoorColliders = new System.Collections.Generic.HashSet<Collider2D>();

    /// <summary>ยกเลิกการชนกับประตู เพื่อให้ตัวบินบินผ่านได้อย่างอิสระ</summary>
    private void IgnoreDoorCollision(Door door, bool ignore = true)
    {
        if (door == null) return;
        var myColliders = GetComponentsInChildren<Collider2D>(true);
        var doorColliders = door.GetComponentsInChildren<Collider2D>(true);
        foreach (var dc in doorColliders)
        {
            if (dc == null) continue;
            foreach (var mc in myColliders)
            {
                if (mc != null && mc != dc)
                {
                    Physics2D.IgnoreCollision(mc, dc, ignore);
                }
            }
            if (ignore)
            {
                ignoredDoorColliders.Add(dc);
            }
            else
            {
                ignoredDoorColliders.Remove(dc);
            }
        }
    }

    /// <summary>คืนค่าการชนกับประตูทั้งหมดเมื่อลงจอด</summary>
    private void RestoreDoorCollisions()
    {
        var myColliders = GetComponentsInChildren<Collider2D>(true);
        foreach (var dc in ignoredDoorColliders)
        {
            if (dc != null)
            {
                foreach (var mc in myColliders)
                {
                    if (mc != null && mc != dc)
                    {
                        Physics2D.IgnoreCollision(mc, dc, false);
                    }
                }
            }
        }
        ignoredDoorColliders.Clear();
    }

    /// <summary>ยกเลิกการชนกับประตูทั้งหมดในฉาก</summary>
    private void IgnoreAllDoors()
    {
        Door[] allDoors = FindObjectsByType<Door>(FindObjectsSortMode.None);
        foreach (var d in allDoors)
        {
            if (d != null)
            {
                IgnoreDoorCollision(d, true);
                if (skywardBreakDoorsOnPierce && d.IsBreakable && !d.IsBroken)
                {
                    d.BreakDoor();
                }
            }
        }
    }

    /// <summary>เรียกเมื่อตัววัวในโหมด Skyward แตะสวิตช์/ปุ่ม ทำให้ได้รับความสามารถทะลุประตูได้</summary>
    private void OnSkywardSwitchTriggered()
    {
        if (skywardSwitchTouched) return;
        skywardSwitchTouched = true;
        Debug.Log($"[{animalName}] Skyward แตะสวิตช์แล้ว! ได้รับความสามารถทะลุประตู (Skyward Door Pierce Active)");
        IgnoreAllDoors();

        if (bounceStage == BounceStage.BouncingToTarget)
        {
            bounceStage = BounceStage.FreeBouncing;
            freeBounceCount = 0;
            freeBounceTimer = 0f;
        }
    }

    /// <summary>ตรวจสอบประตูที่เปิดอยู่ข้างหน้าแบบเรียลไทม์เพื่อไม่ให้ชนติด</summary>
    private void CheckAheadForOpenDoors()
    {
        if (bodyCollider == null) return;
        Vector2 checkDir = FacingDirection;
        if (checkDir.sqrMagnitude < 0.01f) checkDir = ToVector(flyDirection);
        if (checkDir.sqrMagnitude < 0.01f) return;

        float speed = moveSpeed * SpeedMultiplier;
        float castDist = speed * Time.fixedDeltaTime + 0.4f;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(rb.position, 0.4f, checkDir, castDist);
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider == bodyCollider) continue;
            Door door = hit.collider.GetComponentInParent<Door>();
            if (door != null)
            {
                if (flyThroughAnyDoor || door.IsOpen || door.IsBroken ||
                    (skywardSwitchTouched && skywardPierceDoorsAfterSwitch))
                {
                    IgnoreDoorCollision(door, true);
                    if (skywardBreakDoorsOnPierce && door.IsBreakable && !door.IsBroken)
                    {
                        door.BreakDoor();
                    }
                }
            }
        }
    }

    private void TryHandleDoorHover(Door door)
    {
        if (door == null || isDoorHovering || !isFlying) return;
        if (isSurgePatrolling || isPatrolPushing || isBouncing || isHavocBouncing || isHavocPostBouncing) return;

        if (doorHoverRoutine != null)
        {
            StopCoroutine(doorHoverRoutine);
            doorHoverRoutine = null;
        }

        doorHoverRoutine = StartCoroutine(DoorHoverRoutine(door));
    }

    private IEnumerator DoorHoverRoutine(Door door)
    {
        isDoorHovering = true;
        StopMoving();
        rb.linearVelocity = Vector2.zero;
        flyEndTime = float.MaxValue;

        Vector2 hoverPos = rb.position;
        float waitTimer = 0f;

        while (isFlying && door != null && !door.IsOpen && !door.IsBroken && !flyThroughAnyDoor)
        {
            if (maxDoorWaitDuration > 0f && waitTimer >= maxDoorWaitDuration)
            {
                break;
            }

            waitTimer += Time.deltaTime;
            rb.linearVelocity = Vector2.zero;
            rb.position = hoverPos;
            yield return null;
        }

        // เมื่อประตูเปิดแล้ว หรือเปิดโหมดบินทะลุประตู ให้เพิกเฉยต่อการชนทันที
        if (door != null && (door.IsOpen || door.IsBroken || flyThroughAnyDoor))
        {
            IgnoreDoorCollision(door, true);
        }

        isDoorHovering = false;
        doorHoverRoutine = null;

        // ถ้ายังบินอยู่ แต่ประตูปิดค้างจนเกินเวลาที่ตั้งไว้ -> ค่อยลงจอด
        if (isFlying && door != null && !door.IsOpen && !door.IsBroken && !flyThroughAnyDoor)
        {
            Land();
        }
    }
    #endregion
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
    private bool isSwitchHovering = false;
    private Coroutine switchHoverRoutine = null;
    private bool isDoorHovering = false;
    private Coroutine doorHoverRoutine = null;
    private bool isBouncing = false;
    private bool isPatrolPushing = false;
    private bool isHavocBouncing = false;
    private bool isHavocPostBouncing = false;
    private int havocBounceCount = 0;
    private int havocPostBounceCount = 0;
    private Coroutine activeHavocRoutine = null;
    private enum SurgeStage { None, Ascending, Patrolling, SmashingBox, Returning, Descending }
    private SurgeStage surgeStage = SurgeStage.None;
    private bool isSurgePatrolling = false;
    private Vector2 surgeOriginPos;
    private Vector2 surgeDir = Vector2.right;
    private int surgeWallReversesCount = 0;
    private bool surgeHitWall = false;
    private Vector2 surgeWallNormal = Vector2.zero;
    private bool surgeTargetBroken = false;
    private PushableBox surgePendingBoxTarget = null;
    private Coroutine activeSurgeRoutine = null;
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
    private bool skywardSwitchTouched = false;
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

    public bool HoverOnSwitchTouch
    {
        get => hoverOnSwitchTouch;
        set => hoverOnSwitchTouch = value;
    }

    public float DelayAfterSwitchBeforeLand
    {
        get => delayAfterSwitchBeforeLand;
        set => delayAfterSwitchBeforeLand = Mathf.Max(0f, value);
    }

    public bool TriggerAnySwitchOnTouch
    {
        get => triggerAnySwitchOnTouch;
        set => triggerAnySwitchOnTouch = value;
    }

    public bool IsSwitchHovering => isSwitchHovering;

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
        if (potion == PotionType.Surge && surgeFormSprite != null)
            return surgeFormSprite;
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
        if (activeSurgeRoutine != null)
        {
            StopCoroutine(activeSurgeRoutine);
            activeSurgeRoutine = null;
        }
        if (switchHoverRoutine != null)
        {
            StopCoroutine(switchHoverRoutine);
            switchHoverRoutine = null;
        }
        isSwitchHovering = false;
        if (doorHoverRoutine != null)
        {
            StopCoroutine(doorHoverRoutine);
            doorHoverRoutine = null;
        }
        isDoorHovering = false;
        skywardSwitchTouched = false;
    }

    protected override void Update()
    {
        base.Update();

        if (isFlying && !isSwitchHovering && !isPatrolPushing && !isSurgePatrolling && !isHavocBouncing && !isHavocPostBouncing && Time.time >= flyEndTime) Land();

        // ยกสไปรต์ขึ้น/ลงแบบนุ่มๆ
        if (spriteTransform != null)
        {
            Vector3 target = spriteBaseLocalPos + (isFlying ? Vector3.up * liftHeight : Vector3.zero);
            spriteTransform.localPosition = Vector3.Lerp(spriteTransform.localPosition, target, liftSmoothSpeed * Time.deltaTime);
        }

        // หันหน้าซ้าย-ขวาตามทิศทางการเด้ง
        if ((isBouncing || isHavocBouncing || isHavocPostBouncing) && spriteRenderer != null && Mathf.Abs(FacingDirection.x) > 0.01f)
        {
            FaceHorizontal(FacingDirection.x < 0f);
        }
    }

    protected override void FixedUpdate()
    {
        if (isFlying && (passOpenDoors || flyThroughAnyDoor || (isBouncing && skywardSwitchTouched && skywardPierceDoorsAfterSwitch)))
        {
            CheckAheadForOpenDoors();
        }

        if (isPatrolPushing)
        {
            // โหมดลาดตระเวนผลักกล่องจัดการการเคลื่อนที่และฟิสิกส์เองใน PatrolPushRoutine
            UpdateGravityState();
            return;
        }

        if (isSurgePatrolling)
        {
            // โหมด Surge ลาดตระเวนพังกล่อง/ประตูก็จัดการฟิสิกส์และการเคลื่อนที่เองใน SurgePatrolSmashRoutine
            UpdateGravityState();
            return;
        }

        if (isHavocBouncing || isHavocPostBouncing)
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
                        if (sw == null) sw = destTransform.GetComponentInChildren<FloorSwitch>();
                        if (sw == null)
                        {
                            Collider2D nearbyCol = Physics2D.OverlapCircle(destTransform.position, 1.2f);
                            if (nearbyCol != null)
                            {
                                sw = nearbyCol.GetComponentInParent<FloorSwitch>();
                                if (sw == null) sw = nearbyCol.GetComponentInChildren<FloorSwitch>();
                            }
                        }
                        if (sw != null) sw.RemoteToggle();

                        if (skywardPierceDoorsAfterSwitch)
                        {
                            OnSkywardSwitchTriggered();
                        }

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

            // ตรวจจับกำแพงล่วงหน้าแบบเรียลไทม์ด้วย CircleCast เพื่อสะท้อนออกทันทีแบบกระจก (ข้ามประตูเมื่ออยู่ในโหมดทะลุประตู)
            CircleCollider2D circle = bodyCollider as CircleCollider2D;
            float radius = circle != null ? circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * 0.95f : 0.45f;
            float castDist = step + 0.05f;

            RaycastHit2D[] hits = Physics2D.CircleCastAll(rb.position, radius, currentBounceDir, castDist, ~flyOverLayers);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.isTrigger || h.collider.gameObject == gameObject)
                    continue;

                Door door = h.collider.GetComponentInParent<Door>();
                if (door == null) door = h.collider.GetComponentInChildren<Door>();

                if (door != null)
                {
                    bool canPassDoor = flyThroughAnyDoor || door.IsOpen || door.IsBroken ||
                                       (skywardSwitchTouched && skywardPierceDoorsAfterSwitch);
                    if (canPassDoor)
                    {
                        IgnoreDoorCollision(door, true);
                        if (skywardBreakDoorsOnPierce && door.IsBreakable && !door.IsBroken)
                        {
                            door.BreakDoor();
                        }
                        continue;
                    }
                }

                OnBounceHit(h.normal);
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
            if (activeSurgeRoutine != null)
            {
                StopCoroutine(activeSurgeRoutine);
                activeSurgeRoutine = null;
            }

            activeSurgeRoutine = StartCoroutine(SurgePatrolSmashRoutine(aimWorldPoint));
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
        UpdateGravityState();
        bodyCollider.excludeLayers = flyOverLayers; // ไม่ชน Layer เหล่านี้ตอนลอย
        if (shadow != null) shadow.SetActive(true);
        PlayAnimalSound(flySound);

        if (passOpenDoors || flyThroughAnyDoor || (skywardSwitchTouched && skywardPierceDoorsAfterSwitch))
        {
            foreach (var door in FindObjectsByType<Door>(FindObjectsSortMode.None))
            {
                if (door != null && (flyThroughAnyDoor || door.IsOpen || door.IsBroken || (skywardSwitchTouched && skywardPierceDoorsAfterSwitch)))
                {
                    IgnoreDoorCollision(door, true);
                }
            }
        }
    }

    private void Land()
    {
        isFlying = false;
        isSwitchHovering = false;
        isDoorHovering = false;
        skywardSwitchTouched = false;
        if (switchHoverRoutine != null)
        {
            StopCoroutine(switchHoverRoutine);
            switchHoverRoutine = null;
        }
        if (doorHoverRoutine != null)
        {
            StopCoroutine(doorHoverRoutine);
            doorHoverRoutine = null;
        }
        isBouncing = false;
        isPatrolPushing = false;
        isSurgePatrolling = false;
        IsRamming = false;
        isHavocBouncing = false;
        isHavocPostBouncing = false;
        patrolPushStage = PatrolPushStage.None;
        surgeStage = SurgeStage.None;
        currentBounceSpeedBonus = 0f;
        if (activeSurgeRoutine != null)
        {
            StopCoroutine(activeSurgeRoutine);
            activeSurgeRoutine = null;
        }
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
        UpdateGravityState();
        bodyCollider.excludeLayers = (LayerMask)0;
        if (shadow != null) shadow.SetActive(false);
        RestoreDoorCollisions();
    }

    private void TeleportTo(Vector3 destination)
    {
        PlayTeleportSound();

        // เอฟเฟกต์ควันตอนเริ่มวาร์ปจากจุดเดิม
        if (smokeEffectPrefab != null) Instantiate(smokeEffectPrefab, transform.position, Quaternion.identity);

        StopMoving();
        rb.position = destination;
        transform.position = destination;

        // เอฟเฟกต์ควันตอนโผล่มาที่จุดใหม่
        if (smokeEffectPrefab != null) Instantiate(smokeEffectPrefab, destination, Quaternion.identity);

        Debug.Log($"[{animalName}] ควอนตั้มวาร์ป (TP) ไปยังจุด {destination} สำเร็จ!");
    }

    private void PlayTeleportSound()
    {
        AudioClip clip = teleportSound != null ? teleportSound : (AudioManager.Instance != null ? AudioManager.Instance.GetClip("CowTeleport") : null);
        if (clip != null)
        {
            PlayAnimalSound(clip, teleportVolume);
        }
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
        UpdateGravityState();
        flyEndTime = float.MaxValue;
        StopMoving();

        patrolOriginPos = rb.position;
        float targetFlightY = patrolOriginPos.y + Mathf.Max(0.2f, patrolLiftHeight);

        // ตรวจสอบเพดานด้านบนก่อนลอยตัว กันชนทะลุเพดาน
        LayerMask wallMask = patrolObstacleLayers.value != 0 ? patrolObstacleLayers : (LayerMask)(~flyOverLayers);
        RaycastHit2D ceilCheck = Physics2D.Raycast(patrolOriginPos, Vector2.up, patrolLiftHeight + 0.2f, wallMask);
        if (ceilCheck.collider != null && !ceilCheck.collider.isTrigger && !ceilCheck.collider.transform.IsChildOf(transform))
        {
            float maxAllowedY = ceilCheck.point.y - 0.35f;
            if (maxAllowedY > patrolOriginPos.y + 0.05f)
            {
                targetFlightY = Mathf.Min(targetFlightY, maxAllowedY);
            }
        }

        // --- 1. ลอยตัวขึ้นสู่ระดับความสูงบิน (Ascend) พร้อม Safety Timeout ---
        float ascendSpeed = Mathf.Max(2f, patrolSpeed);
        float ascendTimer = 0f;
        while (Mathf.Abs(rb.position.y - targetFlightY) > 0.03f && ascendTimer < 1.0f)
        {
            ascendTimer += Time.fixedDeltaTime;
            float newY = Mathf.MoveTowards(rb.position.y, targetFlightY, ascendSpeed * Time.fixedDeltaTime);
            rb.position = new Vector2(patrolOriginPos.x, newY);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
        }
        rb.position = new Vector2(patrolOriginPos.x, targetFlightY);
        rb.linearVelocity = Vector2.zero;

        // กำหนดทิศทางเริ่มต้น
        patrolDir = (patrolStartDirection == AutoDirection.Left) ? Vector2.left : Vector2.right;
        FacingDirection = patrolDir;
        FaceHorizontal(patrolDir.x < 0f);

        patrolDetectedBox = null;
        patrolHitWall = false;
        patrolWallNormal = Vector2.zero;
        float wallCooldown = 0f;
        float stuckTimer = 0f;
        Vector2 lastRecordedPos = rb.position;
        patrolPushStage = PatrolPushStage.Patrolling;

        Debug.Log($"[{animalName}] เริ่มบินลาดตระเวน ทิศทาง {patrolDir} ความสูงลอยตัว {targetFlightY}");

        // --- 2. บินลาดตระเวน ซ้าย-ขวา ไปเรื่อยๆ จนเจอกล่องหรือชนกำแพงสลับทิศ ---
        while (patrolDetectedBox == null)
        {
            wallCooldown -= Time.fixedDeltaTime;

            float speed = patrolSpeed * SpeedMultiplier;
            // คุมระดับความสูงบินผ่านความเร็วแกน Y อย่างนุ่มนวล ไม่แย่งตำแหน่งกับแกน X
            float targetVy = Mathf.Clamp((targetFlightY - rb.position.y) * 5f, -2.5f, 2.5f);
            rb.linearVelocity = new Vector2(patrolDir.x * speed, targetVy) + ConveyorVelocity;

            // CircleCast ตรวจจับสิ่งกีดขวาง/กล่องล่วงหน้า
            CircleCollider2D circle = bodyCollider as CircleCollider2D;
            float radius = (circle != null) ? circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * 0.9f : 0.45f;
            float checkDist = Mathf.Max(0.12f, speed * Time.fixedDeltaTime + 0.06f);
            LayerMask effectiveBoxMask = boxLayer.value != 0 ? boxLayer : (LayerMask)(1 << 6);
            LayerMask checkMask = patrolObstacleLayers.value != 0 ? (patrolObstacleLayers | effectiveBoxMask) : (LayerMask)(~flyOverLayers | effectiveBoxMask);

            RaycastHit2D hit = Physics2D.CircleCast(rb.position, radius, patrolDir, checkDist, checkMask);
            if (hit.collider != null && !hit.collider.isTrigger && !hit.collider.transform.IsChildOf(transform))
            {
                PushableBox box = hit.collider.GetComponentInParent<PushableBox>();
                if (box != null && !box.IsBroken)
                {
                    patrolDetectedBox = box;
                    break;
                }
                else if (wallCooldown <= 0f)
                {
                    // ต้องเป็นกำแพงแนวตั้งที่ขวางหน้าทิศทางบินเท่านั้น (ไม่นับพื้นด้านล่าง)
                    if (Mathf.Abs(hit.normal.x) > 0.35f && Vector2.Dot(patrolDir, hit.normal) < -0.3f)
                    {
                        patrolHitWall = true;
                        patrolWallNormal = hit.normal;
                    }
                }
            }

            // ตรวจจับอาการติดนิ่งอยู่กับที่ (stuck detection)
            if (Vector2.Distance(rb.position, lastRecordedPos) < 0.03f)
            {
                stuckTimer += Time.fixedDeltaTime;
            }
            else
            {
                stuckTimer = 0f;
                lastRecordedPos = rb.position;
            }

            // ถ้าชนกำแพง หรือติดนิ่งเกิน 0.8 วิ -> สลับทิศทางซ้าย-ขวา
            if ((patrolHitWall && wallCooldown <= 0f) || stuckTimer >= 0.8f)
            {
                patrolDir = -patrolDir;
                FacingDirection = patrolDir;
                FaceHorizontal(patrolDir.x < 0f);

                Vector2 nudge = (patrolWallNormal.sqrMagnitude > 0.01f) ? patrolWallNormal.normalized : -patrolDir;
                rb.position += nudge * 0.08f;

                wallCooldown = 0.35f;
                stuckTimer = 0f;
                lastRecordedPos = rb.position;
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
            // ต้องเป็นกำแพงแนวตั้งที่ขวางทิศทางเดินเท่านั้น ไม่ใช่พื้น
            if (Mathf.Abs(normal.x) > 0.35f && Vector2.Dot(patrolDir, normal) < -0.3f)
            {
                patrolHitWall = true;
                patrolWallNormal = normal;
            }
        }
    }

    #region Surge Potion (Patrol & Smash)
    /// <summary>
    /// ทำงานเมื่อได้ยา Surge (ฟ้า+ส้ม):
    /// 1. ลอยตัวขึ้นตามระดับ surgeLiftHeight
    /// 2. บินลาดตระเวน ซ้าย-ขวา พังกล่องและพังประตูทุกอย่างที่ขวางทาง
    /// 3. เมื่อชนกำแพงแข็งหรือสิ่งกีดขวางที่พังไม่ได้ จะสลับทิศทางซ้าย-ขวา (ตาม surgeMaxWallReverses)
    /// 4. เมื่อติด/ชนกำแพงจนครบกำหนดแล้ว ("จนติด") จะบินกลับไปยังตำแหน่งต้นฉบับเดิม (originPos) แล้วลงจอด
    /// </summary>
    private IEnumerator SurgePatrolSmashRoutine(Vector2? initialAim = null)
    {
        isSurgePatrolling = true;
        IsRamming = true;
        surgeStage = SurgeStage.Ascending;
        TakeOff();
        flyEndTime = float.MaxValue;
        StopMoving();

        surgeOriginPos = rb.position;
        float targetFlightY = surgeOriginPos.y + surgeLiftHeight;

        // ตรวจสอบเพดานด้านบนก่อนลอยตัว กันชนทะลุเพดาน
        LayerMask wallMask = surgeObstacleLayers.value != 0 ? surgeObstacleLayers : (LayerMask)(~flyOverLayers);
        RaycastHit2D ceilCheck = Physics2D.Raycast(surgeOriginPos, Vector2.up, surgeLiftHeight + 0.2f, wallMask);
        if (ceilCheck.collider != null && !ceilCheck.collider.isTrigger && ceilCheck.collider.gameObject != gameObject)
        {
            targetFlightY = Mathf.Min(targetFlightY, ceilCheck.point.y - 0.35f);
        }

        // --- 1. ลอยตัวขึ้นสู่ระดับความสูงบิน (Ascend) ---
        float ascendSpeed = Mathf.Max(2f, surgeSpeed);
        float ascendTimer = 0f;
        while (Mathf.Abs(rb.position.y - targetFlightY) > 0.02f && ascendTimer < 1.0f)
        {
            ascendTimer += Time.fixedDeltaTime;
            float newY = Mathf.MoveTowards(rb.position.y, targetFlightY, ascendSpeed * Time.fixedDeltaTime);
            rb.position = new Vector2(surgeOriginPos.x, newY);
            yield return new WaitForFixedUpdate();
        }
        rb.position = new Vector2(surgeOriginPos.x, targetFlightY);

        // กำหนดทิศทางเริ่มต้น
        if (initialAim.HasValue && Mathf.Abs(initialAim.Value.x - rb.position.x) > 0.1f)
        {
            surgeDir = (initialAim.Value.x < rb.position.x) ? Vector2.left : Vector2.right;
        }
        else
        {
            surgeDir = (surgeStartDirection == AutoDirection.Left) ? Vector2.left : Vector2.right;
        }

        FacingDirection = surgeDir;
        FaceHorizontal(surgeDir.x < 0f);

        surgeWallReversesCount = 0;
        surgeHitWall = false;
        surgeWallNormal = Vector2.zero;
        surgeTargetBroken = false;
        float wallCooldown = 0f;
        float stuckTimer = 0f;
        Vector2 lastRecordedPos = rb.position;
        float totalFlightTime = 0f;

        surgeStage = SurgeStage.Patrolling;
        Debug.Log($"[{animalName}] เริ่ม Surge บินลาดตระเวนซ้าย-ขวา หาเป้าหมายกล่อง/ประตู (ทิศทาง {surgeDir}, ความเร็ว {surgeSpeed})");

        // --- 2. บินลาดตระเวน ซ้าย-ขวา พังกล่อง พังประตู (ถ้าไม่มีกล่องจะบินไปมาซ้าย-ขวาต่อเนื่อง) ---
        while (!surgeTargetBroken)
        {
            totalFlightTime += Time.fixedDeltaTime;
            wallCooldown -= Time.fixedDeltaTime;

            // ตรวจสอบระยะเวลาสูงสุดในกรณีที่ไม่มีกล่อง/ประตู (ถ้าตั้งไว้ > 0)
            if (surgeMaxPatrolDurationWithoutTarget > 0f && totalFlightTime >= surgeMaxPatrolDurationWithoutTarget)
            {
                Debug.Log($"[{animalName}] Surge บินลาดตระเวนจนครบเวลา {surgeMaxPatrolDurationWithoutTarget} วิ (ไม่พบกล่อง/ประตู)");
                break;
            }

            float speed = surgeSpeed;
            // คุมระดับความสูงบินผ่านความเร็วแกน Y อย่างนุ่มนวล ไม่แย่งตำแหน่งกับแกน X
            float targetVy = Mathf.Clamp((targetFlightY - rb.position.y) * 5f, -2.5f, 2.5f);
            rb.linearVelocity = new Vector2(surgeDir.x * speed, targetVy) + ConveyorVelocity;

            // ตรวจจับกล่องที่ชนเข้ามาจาก OnCollisionEnter/Stay
            if (surgePendingBoxTarget != null && !surgePendingBoxTarget.IsBroken)
            {
                PushableBox targetBox = surgePendingBoxTarget;
                surgePendingBoxTarget = null;
                yield return StartCoroutine(SurgeSmashBoxSequence(targetBox));
                if (surgeTargetBroken) break;
            }

            // CircleCast ตรวจจับกล่องหรือประตูด้านหน้าล่วงหน้า
            CircleCollider2D circle = bodyCollider as CircleCollider2D;
            float radius = (circle != null) ? circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * 0.9f : 0.45f;
            float checkDist = Mathf.Max(0.15f, speed * Time.fixedDeltaTime + 0.08f);

            RaycastHit2D hit = Physics2D.CircleCast(rb.position, radius, surgeDir, checkDist, ~flyOverLayers);
            if (hit.collider != null && !hit.collider.isTrigger && hit.collider.gameObject != gameObject)
            {
                PushableBox box = hit.collider.GetComponentInParent<PushableBox>();
                if (box != null && !box.IsBroken)
                {
                    if (surgeBreakBoxes)
                    {
                        yield return StartCoroutine(SurgeSmashBoxSequence(box));
                        if (surgeTargetBroken) break;
                    }
                }

                Door door = hit.collider.GetComponentInParent<Door>();
                if (door != null && !door.IsBroken)
                {
                    if (surgeBreakDoors && door.IsBreakable)
                    {
                        Debug.Log($"[{animalName}] Surge พุ่งชนประตู {door.name} จนแตก!");
                        door.BreakDoor();
                        surgeTargetBroken = true;
                        break;
                    }
                }

                // ถ้าไม่ใช่กล่องและไม่ใช่ประตู และเป็นกำแพงแนวตั้งที่ขวางอยู่ข้างหน้า -> ชนกำแพงสลับทิศทาง
                if (box == null && door == null && wallCooldown <= 0f)
                {
                    if (Mathf.Abs(hit.normal.x) > 0.35f && Vector2.Dot(surgeDir, hit.normal) < -0.3f)
                    {
                        surgeHitWall = true;
                        surgeWallNormal = hit.normal;
                    }
                }
            }

            // ตรวจสอบอาการติดนิ่งอยู่กับที่ (เช่น ชนกำแพงที่เรย์แคสต์ไม่จับ)
            if (Vector2.Distance(rb.position, lastRecordedPos) < 0.03f)
            {
                stuckTimer += Time.fixedDeltaTime;
            }
            else
            {
                stuckTimer = 0f;
                lastRecordedPos = rb.position;
            }

            // ถ้าชนกำแพงแข็ง หรือติดนิ่งเกิน 0.8 วิ -> สลับทิศทางซ้าย-ขวา บินต่อไปเรื่อยๆ
            if ((surgeHitWall && wallCooldown <= 0f) || stuckTimer >= 0.8f)
            {
                surgeWallReversesCount++;
                surgeDir = -surgeDir;
                FacingDirection = surgeDir;
                FaceHorizontal(surgeDir.x < 0f);

                // Nudge แนวนอนออกจากกำแพงเล็กน้อย
                rb.position += new Vector2(surgeDir.x * 0.08f, 0f);

                wallCooldown = 0.35f;
                stuckTimer = 0f;
                lastRecordedPos = rb.position;
                surgeHitWall = false;
                surgeWallNormal = Vector2.zero;

                Debug.Log($"[{animalName}] Surge ชนกำแพง -> สลับทิศทางเป็น {surgeDir} (บินลาดตระเวนซ้าย-ขวาต่อ)");
            }

            yield return new WaitForFixedUpdate();
        }

        // --- 3. บินกลับไปยังตำแหน่งต้นฉบับเดิม (เฉพาะกรณีชนกล่องจนแตก หรือ ประตูจนแตก) ---
        if (surgeTargetBroken)
        {
            // หยุดความเร็วชั่วขณะและหน่วงเวลาสั้นๆ ให้เห็นกล่อง/ประตูแตก
            StopMoving();
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.2f);

            surgeStage = SurgeStage.Returning;
            Debug.Log($"[{animalName}] Surge ทำลายเป้าหมายจนแตกแล้ว -> กำลังบินกลับไปยังตำแหน่งต้นฉบับ {surgeOriginPos}");

            Vector2 returnHorizontalDir = (surgeOriginPos.x < rb.position.x) ? Vector2.left : Vector2.right;
            FacingDirection = returnHorizontalDir;
            FaceHorizontal(returnHorizontalDir.x < 0f);

            float returnSafetyTimer = 0f;
            while (Mathf.Abs(rb.position.x - surgeOriginPos.x) > 0.05f && returnSafetyTimer < 10f)
            {
                returnSafetyTimer += Time.fixedDeltaTime;
                float step = surgeReturnSpeed * Time.fixedDeltaTime;
                Vector2 nextH = Vector2.MoveTowards(rb.position, new Vector2(surgeOriginPos.x, targetFlightY), step);
                rb.MovePosition(nextH);
                yield return new WaitForFixedUpdate();
            }
            rb.MovePosition(new Vector2(surgeOriginPos.x, targetFlightY));

            // --- 4. ลดระดับลงสู่ตำแหน่งต้นฉบับและลงจอด (Descend & Land) ---
            surgeStage = SurgeStage.Descending;
            returnSafetyTimer = 0f;
            while (Mathf.Abs(rb.position.y - surgeOriginPos.y) > 0.03f && returnSafetyTimer < 5f)
            {
                returnSafetyTimer += Time.fixedDeltaTime;
                float step = surgeReturnSpeed * Time.fixedDeltaTime;
                Vector2 nextPos = Vector2.MoveTowards(rb.position, surgeOriginPos, step);
                rb.MovePosition(nextPos);
                yield return new WaitForFixedUpdate();
            }
            rb.MovePosition(surgeOriginPos);
            rb.linearVelocity = Vector2.zero;

            Debug.Log($"[{animalName}] Surge บินกลับถึงตำแหน่งต้นฉบับและลงจอดเรียบร้อยแล้ว");
        }

        surgeStage = SurgeStage.None;
        isSurgePatrolling = false;
        IsRamming = false;
        activeSurgeRoutine = null;
        Land();
    }

    /// <summary>
    /// กระบวนการชนทำลายกล่องของโหมด Surge:
    /// พุ่งชน -> กระเด็นถอยหลังกลางอากาศ -> หยุดตั้งหลัก -> พุ่งกลับไปชนซ้ำ จนครบ surgeHitsToBreakBox ครั้ง -> กล่องแตก!
    /// </summary>
    private IEnumerator SurgeSmashBoxSequence(PushableBox box)
    {
        if (box == null || box.IsBroken) yield break;

        surgeStage = SurgeStage.SmashingBox;
        IsRamming = true;
        PlayAnimalSound(ramSound);
        StopMoving();
        rb.linearVelocity = Vector2.zero;

        // ทำให้แน่ใจว่ากล่องทำลายได้ และซิงค์จำนวนครั้งที่ต้องชนตามที่ตั้งไว้ในตัวสัตว์ (surgeHitsToBreakBox)
        box.IsDestructible = true;
        int targetHits = surgeInstantBreakBox ? 1 : Mathf.Max(1, surgeHitsToBreakBox);
        box.HitsToBreak = targetHits;

        LayerMask wallMask = surgeObstacleLayers.value != 0 ? surgeObstacleLayers : (LayerMask)(~flyOverLayers);
        float flightY = rb.position.y;

        for (int hit = 1; hit <= targetHits; hit++)
        {
            if (box == null || box.IsBroken) break;

            // 1. กระแทกกล่อง (Impact Feedback & Shake)
            Debug.Log($"[{animalName}] Surge พุ่งชนกล่อง {box.name} ครั้งที่ {hit}/{targetHits}!");
            box.TriggerHitFeedback();

            // ชนครบจำนวนแล้ว -> กล่องแตก!
            if (hit >= targetHits)
            {
                Debug.Log($"[{animalName}] Surge พุ่งชนกล่อง {box.name} ครบ {targetHits} ครั้ง จนแตกสมบูรณ์!");
                box.BreakBox();
                surgeTargetBroken = true;
                break;
            }

            // 2. กระเด็นถอยหลังกลางอากาศ (Recoil Backward)
            Vector2 recoilDir = -surgeDir;
            float desiredRecoil = surgeRecoilDistance;

            // ตรวจสอบสิ่งกีดขวาง/กำแพงด้านหลัง กันกระเด็นทะลุกำแพง
            RaycastHit2D wallBehind = Physics2D.Raycast(rb.position, recoilDir, desiredRecoil + 0.35f, wallMask);
            if (wallBehind.collider != null && wallBehind.collider.gameObject != gameObject && wallBehind.collider.gameObject != box.gameObject)
            {
                desiredRecoil = Mathf.Max(0.2f, wallBehind.distance - 0.3f);
            }

            Vector2 recoilStart = rb.position;
            Vector2 recoilTarget = new Vector2(recoilStart.x + recoilDir.x * desiredRecoil, flightY);
            float recoilDuration = Mathf.Max(0.08f, desiredRecoil / Mathf.Max(surgeRecoilSpeed, 0.5f));
            float recoilElapsed = 0f;

            // หันหน้ามองกล่องอยู่ตลอดแม้ตอนกระเด็นถอยหลัง
            FacingDirection = surgeDir;
            FaceHorizontal(surgeDir.x < 0f);

            while (recoilElapsed < recoilDuration)
            {
                recoilElapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(recoilElapsed / recoilDuration);
                // Ease-out ตอนกระเด็นถอยหลัง
                float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);
                rb.position = Vector2.Lerp(recoilStart, recoilTarget, smoothT);
                yield return new WaitForFixedUpdate();
            }
            rb.position = recoilTarget;
            rb.linearVelocity = Vector2.zero;

            // 3. หยุดตั้งหลักกลางอากาศชั่วขณะ (Pause / Hover in mid-air)
            if (surgePauseBeforeRecharge > 0f)
            {
                yield return new WaitForSeconds(surgePauseBeforeRecharge);
            }

            if (box == null || box.IsBroken) break;

            // 4. พุ่งกลับไปชนกล่องอีกรอบ (Charge back forward)
            FacingDirection = surgeDir;
            FaceHorizontal(surgeDir.x < 0f);

            Collider2D boxCol = box.GetComponent<Collider2D>();
            CircleCollider2D catCircle = bodyCollider as CircleCollider2D;
            float catRadius = (catCircle != null) ? catCircle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) : 0.45f;
            float boxHalfWidth = (boxCol != null) ? boxCol.bounds.extents.x : 0.5f;

            float chargeSafetyTimer = 0f;
            float chargeSpeed = Mathf.Max(surgeSpeed * 1.25f, surgeChargeSpeed);

            while (chargeSafetyTimer < 2.0f && box != null && !box.IsBroken)
            {
                chargeSafetyTimer += Time.fixedDeltaTime;

                // คำนวณระยะห่างระหว่างตัวสัตว์กับกล่องในแนวนอน
                float currentDistX = (surgeDir.x > 0f)
                    ? (box.transform.position.x - boxHalfWidth) - (rb.position.x + catRadius)
                    : (rb.position.x - catRadius) - (box.transform.position.x + boxHalfWidth);

                if (currentDistX <= 0.08f)
                {
                    // ชนถึงตัวกล่องแล้ว
                    break;
                }

                float vy = Mathf.Clamp((flightY - rb.position.y) * 5f, -2.5f, 2.5f);
                rb.linearVelocity = new Vector2(surgeDir.x * chargeSpeed, vy) + ConveyorVelocity;
                yield return new WaitForFixedUpdate();
            }

            rb.linearVelocity = Vector2.zero;
        }

        if (!surgeTargetBroken)
        {
            surgeStage = SurgeStage.Patrolling;
        }
    }

    /// <summary>
    /// จัดการการชนของโหมด Surge (พังกล่อง พังประตู หรือตรวจจับกำแพงแข็งเพื่อสลับทิศหรือติด)
    /// </summary>
    private void HandleSurgeCollision(Collision2D collision)
    {
        if (surgeStage != SurgeStage.Patrolling) return;
        if (collision.gameObject.GetComponentInParent<TestAnimal>() != null) return;

        // 1. ตรวจสอบกล่อง
        PushableBox box = collision.gameObject.GetComponentInParent<PushableBox>();
        if (box != null && !box.IsBroken)
        {
            if (surgeBreakBoxes)
            {
                surgePendingBoxTarget = box;
                return;
            }
        }

        // 2. ตรวจสอบประตู
        Door door = collision.gameObject.GetComponentInParent<Door>();
        if (door != null && !door.IsBroken)
        {
            if (surgeBreakDoors && door.IsBreakable)
            {
                Debug.Log($"[{animalName}] Surge ชนประตู {door.name} จนแตก!");
                door.BreakDoor();
                surgeTargetBroken = true;
                return;
            }
        }

        // 3. กำแพงหรือสิ่งกีดขวางที่พังไม่ได้ (ชนแล้วจะสลับทิศทางเพื่อบินซ้าย-ขวาต่อ ไม่ใช่กลับที่เดิม)
        Vector2 normal = Vector2.zero;
        int count = collision.contactCount;
        for (int i = 0; i < count; i++)
        {
            normal += collision.contacts[i].normal;
        }

        if (normal.sqrMagnitude > 0.01f)
        {
            normal.Normalize();
            // ต้องเป็นกำแพงแนวตั้งที่ขวางหน้าทิศทางการบินเท่านั้น (ไม่นับพื้นหรือเพดาน)
            if (Mathf.Abs(normal.x) > 0.35f && Vector2.Dot(surgeDir, normal) < -0.3f)
            {
                surgeHitWall = true;
                surgeWallNormal = normal;
            }
        }
    }
    #endregion

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

        // วาดระดับความสูงและเส้นทางบินลาดตระเวนของ Surge (สีฟ้าอมส้ม)
        Vector3 surgeBase = Application.isPlaying ? (Vector3)surgeOriginPos : transform.position;
        Vector3 surgeFlightPos = surgeBase + Vector3.up * surgeLiftHeight;
        Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.9f);
        Gizmos.DrawLine(surgeBase, surgeFlightPos);
        Gizmos.DrawWireSphere(surgeFlightPos, 0.25f);
        Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
        Gizmos.DrawLine(surgeFlightPos + Vector3.left * 2f, surgeFlightPos + Vector3.right * 2f);

        // วาดจุดหมายเฉพาะของ Skyward (สีฟ้า)
        Transform skyDest = (skywardDestination != null) ? skywardDestination : targetDestination;
        if (skyDest != null)
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.8f); // สีฟ้า Skyward
            Gizmos.DrawLine(transform.position, skyDest.position);
            Gizmos.DrawWireSphere(skyDest.position, 0.3f);
        }

        // วาดจุดหมายเฉพาะของ Havoc (สีม่วง - แยกอิสระจาก Skyward)
        Transform hDest = GetHavocDestination();
        if (hDest != null && hDest != skyDest)
        {
            Gizmos.color = new Color(0.85f, 0.2f, 1f, 0.9f); // สีม่วง Havoc
            Gizmos.DrawLine(transform.position, hDest.position);
            Gizmos.DrawWireSphere(hDest.position, 0.35f);
        }
    }
#endif
    #endregion

    #region Havoc API
    /// <summary>
    /// หาจุดหมายปลายทางเฉพาะของ Havoc (แยกอิสระจาก Skyward 100% ไม่ใช้จุดร่วมกันเด็ดขาด)
    /// </summary>
    private Transform GetHavocDestination()
    {
        // 1. จุดที่ลากใส่ใน Inspector ของ Havoc Destination
        if (havocDestination != null) return havocDestination;

        // 2. ค้นหาวัตถุลูกหรือในฉากที่มีชื่อว่า Havoc point / Havoc Point
        Transform child = transform.Find("Havoc point");
        if (child == null) child = transform.Find("Havoc Point");
        if (child == null) child = transform.Find("HavocDestination");
        if (child != null) return child;

        GameObject sceneObj = GameObject.Find("Havoc point");
        if (sceneObj == null) sceneObj = GameObject.Find("Havoc Point");
        if (sceneObj != null) return sceneObj.transform;

        // 3. ไม่ใช้ร่วมกับ skywardDestination เด็ดขาด
        if (targetDestination != null && targetDestination != skywardDestination)
        {
            return targetDestination;
        }

        return null;
    }

    /// <summary>
    /// ฟังก์ชันสะท้อนออกจากกำแพงของโหมด Havoc (รองรับทั้งช่วงก่อนวาร์ปและช่วงเด้งต่อหลังวาร์ป)
    /// </summary>
    private void OnHavocBounceHit(Vector2 normal)
    {
        if ((!isHavocBouncing && !isHavocPostBouncing) || normal.sqrMagnitude < 0.0001f) return;
        normal.Normalize();

        if (Vector2.Dot(currentBounceDir, normal) < -0.05f)
        {
            currentBounceDir = Vector2.Reflect(currentBounceDir, normal).normalized;
            FacingDirection = currentBounceDir;
            FaceHorizontal(currentBounceDir.x < 0f);

            rb.position += normal * 0.06f;

            OnBounce();

            if (isHavocBouncing)
            {
                havocBounceCount++;
                Debug.Log($"[{animalName}] Havoc เด้งก่อนวาร์ปครั้งที่ {havocBounceCount}/{havocBouncesBeforeTeleport}");
            }
            else if (isHavocPostBouncing)
            {
                havocPostBounceCount++;
                Debug.Log($"[{animalName}] Havoc เด้งต่อหลังวาร์ปครั้งที่ {havocPostBounceCount}/{havocPostTeleportBounces}");
            }

            float speed = moveSpeed * SpeedMultiplier;
            rb.linearVelocity = currentBounceDir * speed + ConveyorVelocity;
        }
    }

    /// <summary>
    /// โหมด Havoc: เด้งสะท้อนกำแพง 5 ครั้ง -> วาร์ปไปยัง Havoc Destination -> เด้งสะท้อนต่อในบริเวณใหม่ -> ลงจอด
    /// </summary>
    private IEnumerator HavocBounceRoutine()
    {
        isHavocBouncing = true;
        isHavocPostBouncing = false;
        isBouncing = true;
        havocBounceCount = 0;
        havocPostBounceCount = 0;
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

        Debug.Log($"[{animalName}] เริ่ม Havoc: ทิศทาง {currentBounceDir}, กำหนดเด้งก่อนวาร์ป {havocBouncesBeforeTeleport} ครั้ง");

        // --- 1. เด้งสะท้อนกำแพงก่อนวาร์ป (ค่าเริ่มต้น 5 ครั้ง) ---
        while (isHavocBouncing && havocBounceCount < havocBouncesBeforeTeleport)
        {
            yield return null;
        }

        // เด้งครบก่อนวาร์ปแล้ว หยุดการเคลื่อนที่ชั่วขณะ
        isHavocBouncing = false;
        rb.linearVelocity = Vector2.zero;

        // --- 2. หน่วงเวลาสั้นๆ ให้เห็นการชนกำแพงครั้งสุดท้าย ---
        if (delayBeforeTeleport > 0f)
        {
            yield return new WaitForSeconds(delayBeforeTeleport);
        }

        // --- 3. วาร์ป (Teleport) ไปยังจุดหมายเฉพาะของ Havoc ---
        dest = GetHavocDestination();
        if (dest != null)
        {
            Debug.Log($"[{animalName}] Havoc เด้งครบ {havocBouncesBeforeTeleport} ครั้งแล้ว! วาร์ปไปยังจุดหมาย Havoc: {dest.name} ({dest.position})");
            TeleportTo(dest.position);

            // --- 4. สับสวิตช์ FloorSwitch ที่อยู่ ณ จุดวาร์ป (ถ้ามี) ---
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
            Debug.LogWarning($"[{animalName}] Havoc เด้งครบ {havocBouncesBeforeTeleport} ครั้งแล้ว แต่ไม่พบจุดเป้าหมาย Havoc สำหรับวาร์ป (โปรดตั้งค่าในช่อง Havoc Destination ของ Inspector)!");
        }

        // --- 5. วาร์ปไปแล้วเด้งต่อนะ (Post-Teleport Bouncing) ---
        if (bounceAfterTeleport)
        {
            isHavocPostBouncing = true;
            havocPostBounceCount = 0;
            float postTimer = 0f;

            // สุ่มมุมพุ่งตัวเด้งต่อจากจุดที่วาร์ปมาถึง
            float postAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            currentBounceDir = new Vector2(Mathf.Cos(postAngle), Mathf.Sin(postAngle)).normalized;
            FacingDirection = currentBounceDir;
            FaceHorizontal(currentBounceDir.x < 0f);

            speed = moveSpeed * SpeedMultiplier;
            rb.linearVelocity = currentBounceDir * speed + ConveyorVelocity;

            Debug.Log($"[{animalName}] Havoc วาร์ปเสร็จแล้ว -> พุ่งตัวเด้งต่อ! (กำหนดเด้ง {havocPostTeleportBounces} ครั้ง / สูงสุด {havocPostTeleportBounceDuration} วิ)");

            while (isHavocPostBouncing)
            {
                postTimer += Time.deltaTime;

                bool countDone = havocPostTeleportBounces > 0 && havocPostBounceCount >= havocPostTeleportBounces;
                bool timeDone = havocPostTeleportBounceDuration > 0f && postTimer >= havocPostTeleportBounceDuration;

                if (countDone || timeDone)
                {
                    Debug.Log($"[{animalName}] Havoc สิ้นสุดการเด้งต่อหลังวาร์ป (เด้งไป {havocPostBounceCount} ครั้ง, เวลา {postTimer:F1} วิ)");
                    break;
                }

                yield return null;
            }

            isHavocPostBouncing = false;
            rb.linearVelocity = Vector2.zero;
        }

        // --- 6. ลงจอดสู่พื้น ---
        if (landAfterPostTeleport)
        {
            Land();
        }

        isBouncing = false;
        activeHavocRoutine = null;
    }
    #endregion
}