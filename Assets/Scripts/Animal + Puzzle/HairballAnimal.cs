using System.Collections;
using UnityEngine;

/// <summary>
/// สัตว์ทดลองตัวที่ 1: ตัวอ้วกก้อนขน (ใช้ยาอาเจียน) = แมว
/// ความสามารถ: อ้วกก้อนขนออกไปตามทิศ ก้อนขนไปตกที่ปลายระยะ (หรือชนกำแพง) แล้วนอนอยู่สักพัก
/// ก้อนขนที่นอนอยู่กดสวิตช์ได้ (เป็นตุ้มถ่วง) เหมาะกับสวิตช์ที่ตัวเองเดินไปไม่ถึง/ต้องมีของวางทับค้างไว้
/// โหมดอัตโนมัติ: ได้ยาแล้วอ้วกไปทิศที่ตั้งไว้ใน Inspector เอง
/// ยาผสม:
///   Skyward (เขียว+ฟ้า) = อ้วกก้อนขนลอยขึ้นข้างบน
///   Havoc   (เขียว+ส้ม) = กระหน่ำกระสุนอ้วก (ก้อนขนหลายก้อนแตกกระจาย)
///   Surge   (ฟ้า+ส้ม)   = อ้วกเป็นก้อนใหญ่ยักษ์
/// </summary>
public class HairballAnimal : TestAnimal
{
    private enum VomitMode { Normal, Skyward, Barrage, Giant, Bubble }

    [Header("Test Mode")]
    [Tooltip("โหมดเริ่มต้น (ถ้าไม่ติ๊ก Require Potion สามารถเลือกทดสอบ Bubble / Skyward / Barrage / Giant ได้เลย)")]
    [SerializeField] private VomitMode initialMode = VomitMode.Normal;

    #region Vomit Settings
    [Header("Vomit (อ้วกก้อนขน)")]
    [Tooltip("ลาก Prefab ก้อนขนที่มี Hairball.cs มาใส่")]
    [SerializeField] private Hairball hairballPrefab;

    [Tooltip("ระยะที่ก้อนขนเกิดห่างจากตัวสัตว์ (ตรงปาก)")]
    [SerializeField] private float mouthDistance = 0.6f;

    [Tooltip("ความเร็วที่ก้อนขนพุ่งออกไป")]
    [SerializeField] private float hairballSpeed = 8f;

    [Tooltip("ระยะบินสูงสุดของก้อนขน (หน่วย Unity)")]
    [SerializeField] private float hairballRange = 5f;

    [Tooltip("ก้อนขนนอนอยู่กี่วินาทีหลังตกถึงพื้น ก่อนจะหายไป")]
    [SerializeField] private float hairballLingerSeconds = 6f;

    [Tooltip("ติ๊กไว้ = ตอนอ้วกจะหยุดเดินทันที")]
    [SerializeField] private bool stopMovingWhenVomiting = true;

    [Header("Audio (เสียงเฉพาะของแมวก้อนขน)")]
    [Tooltip("เสียงตอนอ้วก/คายก้อนขน")]
    [SerializeField] private AudioClip vomitSound;

    [Tooltip("เสียงตอนเป่าฟองสบู่")]
    [SerializeField] private AudioClip bubbleSound;

    [Tooltip("เสียงตอนฟองสบู่แตก")]
    [SerializeField] private AudioClip bubblePopSound;

    [Tooltip("เสียงตอนเริ่มใช้ Skyward (ครอบฟองสบู่ลอยตัว)")]
    [SerializeField] private AudioClip skywardSound;

    [Range(0f, 1f)]
    [Tooltip("ระดับความดังของเสียง Skyward (0 - 1)")]
    [SerializeField] private float skywardVolume = 1f;

    [Tooltip("เสียงตอนเด้งตอนอยู่ในฟองสบู่ Skyward (เสียงเหมือน Skyward วัว)")]
    [SerializeField] private AudioClip skywardBounceSound;

    [Range(0f, 1f)]
    [Tooltip("ระดับความดังของเสียงตอนเด้งในฟองสบู่ (0 - 1)")]
    [SerializeField] private float skywardBounceVolume = 1f;
    #endregion

    #region Bubble Settings (ฟ้า/Light: เป่าฟองสบู่ดันกล่อง)
    [Header("Bubble (ฟ้า/Light: เป่าฟองสบู่ดันกล่อง)")]
    [Tooltip("ลาก Prefab ฟองสบู่มาใส่ (ถ้าเว้นว่างจะค้นหา Bubble.prefab ในโปรเจกต์ให้อัตโนมัติ)")]
    [SerializeField] private GameObject bubblePrefab;

    [Tooltip("รูปสไปรต์ตัวแมวเมื่อได้ยาน้ำเงิน/ฟ้า (ถ้าเว้นว่างจะใช้รูปปัจจุบัน)")]
    [SerializeField] private Sprite bubbleCatSprite;

    [Tooltip("รูปสไปรต์ฟองสบู่ (กรณีอยากเปลี่ยนรูปจากใน Prefab)")]
    [SerializeField] private Sprite bubbleSprite;

    [Tooltip("Prefab เอฟเฟกต์แตกของฟองสบู่ตอนชนกล่องหรือกำแพง (Particle / Pop effect)")]
    [SerializeField] private GameObject bubblePopEffectPrefab;

    [Header("Bubble Push & Physics (แรงผลักและแรงโน้มถ่วง)")]
    [Tooltip("ระยะที่ฟองสบู่ 1 ลูกผลักกล่องไปได้ (ลูกละเบาๆ เช่น 0.35 หน่วย)")]
    [SerializeField] private float bubblePushDistance = 0.35f;

    [Tooltip("ความเร็วในการเลื่อนกล่องตอนฟองสบู่ผลัก (หน่วย m/s เช่น 3.5)")]
    [SerializeField] private float bubblePushSpeed = 3.5f;

    [Tooltip("แรงโน้มถ่วงของฟองสบู่ (0 = ลอยตรงๆ, ค่าน้อยๆ เช่น 0.05 = ลอยเอื่อยๆ ค่อยๆ ตก, ค่าติดลบ = ลอยขึ้น)")]
    [SerializeField] private float bubbleGravityScale = 0.05f;

    [Tooltip("ติ๊กไว้ = ฟองสบู่แตกทันทีเมื่อชนกล่องและผลักกล่องแล้ว")]
    [SerializeField] private bool bubblePopOnPush = true;

    [Tooltip("ติ๊กไว้ = ฟองสบู่แตกทันทีเมื่อชนกำแพง/พื้น/สิ่งกีดขวาง")]
    [SerializeField] private bool bubblePopOnObstacle = true;

    [Header("Bubble Shots & Bursts (จำนวนลูกและรอบที่ยิง)")]
    [Tooltip("จำนวนฟองสบู่ต่อ 1 ชุด (ค่าเริ่มต้น 3 เม็ด)")]
    [SerializeField, Min(1)] private int bubblesPerSet = 3;

    [Tooltip("จำนวนรอบ/ชุดที่เป่าออกไป (ค่าเริ่มต้น 2 ชุด)")]
    [SerializeField, Min(1)] private int bubbleBurstCount = 2;

    [Tooltip("เวลาหน่วงระหว่างฟองสบู่แต่ละลูกในชุดเดียวกัน (วินาที)")]
    [SerializeField] private float delayBetweenBubbles = 0.15f;

    [Tooltip("เวลาหน่วงระหว่างรอบ/ชุด (วินาที)")]
    [SerializeField] private float delayBetweenBursts = 0.45f;

    [Header("Bubble Flight (การลอยของฟองสบู่)")]
    [Tooltip("ความเร็วที่ฟองสบู่พุ่งออกไป")]
    [SerializeField] private float bubbleSpeed = 6f;

    [Tooltip("ระยะบินสูงสุดของฟองสบู่")]
    [SerializeField] private float bubbleRange = 8f;

    [Tooltip("ฟองสบู่ลอยอยู่นานกี่วินาทีก่อนจะแตกเอง")]
    [SerializeField] private float bubbleLingerSeconds = 3f;

    [Tooltip("การกระจายมุมเล็กน้อยของฟองสบู่ (องศา) เพื่อให้ดูเป็นธรรมชาติ (0 = เส้นตรงเป๊ะ)")]
    [SerializeField] private float bubbleSpreadAngle = 4f;
    #endregion

    #region Combo Settings
    [Header("Skyward (เขียว+ฟ้า): ครอบฟองสบู่ใส่ตัวเองลอยไปมา เด้งกำแพงเหมือน Skyward วัวแต่ความเร็วไม่เพิ่ม")]
    [Tooltip("Prefab สำหรับครอบตัวแมวตอนใช้ Skyward (ลาก Prefab ฟองสบู่หรือเอฟเฟกต์ที่ต้องการมาใส่ได้เลย ถ้าเว้นว่างจะสร้างจาก bubbleSprite หรือ bubblePrefab ให้อัตโนมัติ)")]
    [SerializeField] private GameObject skywardBubblePrefab;

    [Tooltip("จุดเป้าหมายปลายทางสำหรับ Skyward (ถ้ามี แมวจะลอยมุ่งหน้าไปจุดนี้)")]
    [SerializeField] private Transform skywardDestination;

    [Tooltip("ความเร็วในการลอยตัวในฟองสบู่ (ความเร็วคงที่ไม่เพิ่มขึ้นตามที่ผู้เล่นต้องการ)")]
    [SerializeField] private float skywardBubbleSpeed = 5.5f;

    [Tooltip("ระยะเวลาลอยตัวในฟองสบู่ (วินาที, 0 = ไม่จำกัดเวลาจนกว่าจะเด้งครบครั้ง)")]
    [SerializeField] private float skywardBubbleDuration = 8f;

    [Tooltip("จำนวนครั้งสูงสุดที่เด้งกำแพงก่อนฟองสบู่แตกและลงสู่พื้น (0 = ลอยตามเวลา skywardBubbleDuration)")]
    [SerializeField] private int skywardMaxBounces = 10;

    [Tooltip("มุมปล่อยตัวเริ่มต้นตอนลอยฟองสบู่ (องศาเทียบกับแนวนอน เช่น 45 = เฉียงขึ้น 45 องศา)")]
    [SerializeField, Range(15f, 75f)] private float skywardLaunchAngle = 45f;

    [Tooltip("ขนาดของฟองสบู่ที่ครอบตัวแมว (ตัวคูณขนาด)")]
    [SerializeField] private float skywardBubbleShieldScale = 1.6f;

    [Tooltip("ตำแหน่งชดเชยของฟองสบู่เทียบกับตัวแมว")]
    [SerializeField] private Vector2 skywardBubbleOffset = Vector2.zero;

    [Tooltip("ความโปร่งแสงของฟองสบู่ (Alpha 0-1, มีผลเมื่อสร้างจาก Sprite)")]
    [SerializeField, Range(0.1f, 1f)] private float skywardBubbleAlpha = 0.85f;

    [Tooltip("เปิด/ปิดแอนิเมชันขยับพริ้วไหวของฟองสบู่ (Wobble)")]
    [SerializeField] private bool skywardAnimateBubble = true;

    [Tooltip("ติ๊กไว้ = ฟองสบู่แมวสามารถผลักกล่องเบาๆ ได้ตอนลอยชนกล่อง")]
    [SerializeField] private bool skywardPushBoxesOnBounce = true;

    [Tooltip("ระยะที่ผลักกล่องเมื่อลอยชน")]
    [SerializeField] private float skywardBoxPushDistance = 0.35f;

    [Tooltip("Layer สิ่งกีดขวาง/หลุม ที่สามารถลอยข้ามได้ขณะอยู่ในฟองสบู่ (ไม่ชนและไม่ตก)")]
    [SerializeField] private LayerMask skywardFlyOverLayers;

    [Tooltip("Layer สิ่งกีดขวาง/กำแพง ที่ฟองสบู่ชนแล้วจะเด้งสะท้อน (ถ้าเว้นว่างจะเด้งกับทุกกำแพงที่ไม่ใช่ flyOverLayers)")]
    [SerializeField] private LayerMask skywardBounceLayers;

    [Tooltip("รูปสไปรต์ตัวแมวเมื่อได้ยา Skyward (ถ้าเว้นว่างจะใช้ Powered Up Sprite ปกติ)")]
    [SerializeField] private Sprite skywardCatSprite;

    [SerializeField, HideInInspector] private GameObject skywardHairballPrefab;
    [SerializeField, HideInInspector] private Sprite skywardHairballSprite;
    [SerializeField, HideInInspector] private float skywardRange = 4f;
    [SerializeField, HideInInspector] private float skywardSpeed = 4f;

    [Header("Mass (มวลก้อนขน ใช้กับสวิตช์ที่ต้องใช้น้ำหนัก)")]
    [SerializeField] private float normalMass = 1f;
    [SerializeField] private float barrageMass = 0.5f;
    [SerializeField] private float giantMass = 3f;

    [Header("Havoc (เขียว+ส้ม): กระหน่ำกระสุนอ้วก (พังกล่องและประตู)")]
    [Tooltip("Prefab ก้อนขนแยกเฉพาะสำหรับ Havoc (กระหน่ำกระสุน) ถ้าเว้นว่างจะใช้ Hairball Prefab ปกติ")]
    [SerializeField] private GameObject barrageHairballPrefab;

    [Tooltip("รูปสไปรต์ตัวแมวเมื่อได้ยา Havoc (ถ้าเว้นว่างจะใช้ Powered Up Sprite ปกติ)")]
    [SerializeField] private Sprite barrageCatSprite;

    [Tooltip("ติ๊กไว้ = กระสุนก้อนขน Havoc สามารถพังกล่องและประตูได้")]
    [SerializeField] private bool havocBreaksObstacles = true;

    [Tooltip("จำนวนก้อนขนที่กระหน่ำออกไป")]
    [SerializeField] private int barrageCount = 5;

    [Tooltip("มุมกระจายรวมของกระสุน (องศา)")]
    [SerializeField] private float barrageSpreadAngle = 60f;

    [Tooltip("ระยะบินของกระสุนแต่ละก้อน (ใกล้กว่าปกติ)")]
    [SerializeField] private float barrageRange = 3.5f;

    [Tooltip("ก้อนขนกระสุนนอนอยู่กี่วินาที")]
    [SerializeField] private float barrageLingerSeconds = 3f;

    [Header("Surge (ฟ้า+ส้ม): อ้วกเป็นก้อนใหญ่ยักษ์")]
    [Tooltip("Prefab ก้อนขนแยกเฉพาะสำหรับ Surge (อ้วกยักษ์) ถ้าเว้นว่างจะใช้ Hairball Prefab ปกติ")]
    [SerializeField] private GameObject giantHairballPrefab;

    [Tooltip("รูปสไปรต์ตัวแมวเมื่อได้ยา Surge (ถ้าเว้นว่างจะใช้ Powered Up Sprite ปกติ)")]
    [SerializeField] private Sprite giantCatSprite;

    [Tooltip("ขยายขนาดก้อนขนกี่เท่า")]
    [SerializeField] private float giantScale = 2.5f;

    [Tooltip("ความเร็วก้อนใหญ่ (หนักกว่า เลยช้ากว่า)")]
    [SerializeField] private float giantSpeed = 5f;

    [Tooltip("ก้อนใหญ่นอนอยู่กี่วินาที")]
    [SerializeField] private float giantLingerSeconds = 12f;

    [Tooltip("ติ๊กไว้ = อ้วกยักษ์สามารถพังกล่องและประตูที่พังได้")]
    [SerializeField] private bool giantBreaksObstacles = true;

    [Tooltip("ความสูงชดเชยเมื่ออ้วกก้อนยักษ์ (กันก้อนใหญ่จมพื้น)")]
    [SerializeField] private float giantSpawnHeightOffset = 0.65f;

    [Tooltip("ระยะห่างจากตัวเพิ่มเติมเมื่ออ้วกก้อนยักษ์")]
    [SerializeField] private float giantSpawnForwardOffset = 0.5f;

    [Header("Gravity (แรงโน้มถ่วง)")]
    [Tooltip("แรงโน้มถ่วงของก้อนขนปกติ/กระสุน/ก้อนยักษ์ (1 = ปกติ)")]
    [SerializeField] private float defaultGravityScale = 1f;

    [Tooltip("แรงโน้มถ่วงของก้อนขน Skyward (0 = ลอยขึ้นข้างบนตามความสามารถ)")]
    [SerializeField] private float skywardGravityScale = 0f;
    #endregion

    #region Auto
    [Header("Auto (โหมดอัตโนมัติ)")]
    [Tooltip("ทิศที่จะอ้วกก้อนขนไปเมื่อได้ยา")]
    [SerializeField] private AutoDirection vomitDirection = AutoDirection.Right;

    protected override IEnumerator AutoRoutine()
    {
        if (mode == VomitMode.Bubble)
        {
            Vector2 dir = GetFacingDirectionForBubble();
            yield return BubbleRoutine(dir);
            yield break;
        }

        if (mode == VomitMode.Skyward)
        {
            yield return SkywardBubbleRoutine();
            yield break;
        }

        Vector2 defaultDir = ToVector(vomitDirection);
        ExecuteAbility(rb.position + defaultDir * 5f);
        yield break;
    }
    #endregion

    #region Potion & State
    public GameObject SkywardBubblePrefab { get => skywardBubblePrefab; set => skywardBubblePrefab = value; }
    public float SkywardBubbleSpeed { get => skywardBubbleSpeed; set => skywardBubbleSpeed = value; }
    public float SkywardBubbleDuration { get => skywardBubbleDuration; set => skywardBubbleDuration = value; }
    public int SkywardMaxBounces { get => skywardMaxBounces; set => skywardMaxBounces = value; }
    public bool IsSkywardFloating => isSkywardFloating;
    public bool HavocBreaksObstacles { get => havocBreaksObstacles; set => havocBreaksObstacles = value; }
    public override bool IsAirborne => isSkywardFloating || base.IsAirborne;

    private VomitMode mode = VomitMode.Normal;
    private bool isSkywardFloating = false;
    private Vector2 currentBounceDir = Vector2.up;
    private int skywardBounceCount = 0;
    private Coroutine activeSkywardRoutine = null;
    private GameObject activeBubbleShield = null;
    private SpriteRenderer bubbleShieldRenderer = null;
    private Vector3 bubbleBaseScale = Vector3.one;
    private Collider2D bodyCollider;

    protected override PotionType RequiredPotion => PotionType.Vomit;

    public override bool AcceptsPotion(PotionType potion)
    {
        return potion == PotionType.Vomit || potion == PotionType.Light || IsComboPotion(potion);
    }

    protected override void Awake()
    {
        base.Awake();
        bodyCollider = GetComponent<Collider2D>();
        mode = initialMode;
        EnsureSkywardHairballPrefab();
        EnsureBubblePrefab();
        ResolveSkywardSound();
        ResolveSkywardBounceSound();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (skywardSound == null) ResolveSkywardSound();
        if (skywardBounceSound == null) ResolveSkywardBounceSound();
    }
#endif

    private void OnDisable()
    {
        if (activeSkywardRoutine != null)
        {
            StopCoroutine(activeSkywardRoutine);
            activeSkywardRoutine = null;
        }
        if (isSkywardFloating)
        {
            EndSkywardBubble();
        }
    }

    protected override void Update()
    {
        base.Update();
        if (isSkywardFloating)
        {
            UpdateBubbleShieldVisual();
            if (Mathf.Abs(currentBounceDir.x) > 0.01f)
            {
                FaceHorizontal(currentBounceDir.x < 0f);
            }
        }
    }

    protected override void FixedUpdate()
    {
        if (isSkywardFloating)
        {
            HandleSkywardBubblePhysics();
            return;
        }
        base.FixedUpdate();
    }

    private void EnsureSkywardHairballPrefab()
    {
        if (skywardHairballPrefab == null)
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Hairball Skyward t:Prefab");
            if (guids == null || guids.Length == 0)
                guids = UnityEditor.AssetDatabase.FindAssets("Vomit Skyward t:Prefab");
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                skywardHairballPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
#endif
        }
    }

    private void EnsureBubblePrefab()
    {
        if (bubblePrefab == null)
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Bubble t:Prefab");
            if (guids != null && guids.Length > 0)
            {
                foreach (var g in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    if (path.Contains("Mutant") || path.Contains("Projectile"))
                    {
                        bubblePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        break;
                    }
                }
                if (bubblePrefab == null && guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    bubblePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
#endif
        }
    }

    protected override void OnPotionDrunk(PotionType potion)
    {
        switch (potion)
        {
            case PotionType.Light: mode = VomitMode.Bubble; break;
            case PotionType.Skyward: mode = VomitMode.Skyward; break;
            case PotionType.Havoc: mode = VomitMode.Barrage; break;
            case PotionType.Surge: mode = VomitMode.Giant; break;
            default: mode = VomitMode.Normal; break;
        }
        Debug.Log($"[{animalName}] โหมดอ้วก: {mode}");
    }

    protected override Sprite GetCustomPoweredUpSprite(PotionType potion)
    {
        switch (potion)
        {
            case PotionType.Light: return bubbleCatSprite;
            case PotionType.Skyward: return skywardCatSprite;
            case PotionType.Havoc: return barrageCatSprite;
            case PotionType.Surge: return giantCatSprite;
            default: return null;
        }
    }
    #endregion

    #region Ability
    protected override bool PerformAbility(Vector2 aimWorldPoint)
    {
        if (mode == VomitMode.Bubble)
        {
            Vector2 aimOffset = aimWorldPoint - rb.position;
            if (aimOffset.sqrMagnitude < 0.0001f) aimOffset = FacingDirection;
            Vector2 fireDir = new Vector2(Mathf.Sign(aimOffset.x != 0 ? aimOffset.x : (FacingDirection.x != 0 ? FacingDirection.x : 1f)), 0f);
            FaceDirection(fireDir);
            if (stopMovingWhenVomiting) StopMoving();
            StartCoroutine(BubbleRoutine(fireDir));
            return true;
        }

        if (mode == VomitMode.Skyward)
        {
            if (isSkywardFloating) return false;
            if (stopMovingWhenVomiting) StopMoving();
            if (activeSkywardRoutine != null) StopCoroutine(activeSkywardRoutine);
            activeSkywardRoutine = StartCoroutine(SkywardBubbleRoutine());
            return true;
        }

        GameObject activePrefab = GetActivePrefabForMode(mode);
        if (activePrefab == null)
        {
            Debug.LogWarning($"[{animalName}] ยังไม่ได้ลาก Hairball Prefab ใส่ช่อง Hairball Prefab หรือ Skyward Prefab");
            return false;
        }

        Vector2 dir = aimWorldPoint - rb.position;
        if (dir.sqrMagnitude < 0.0001f) dir = FacingDirection;
        dir.Normalize();
        FacingDirection = dir;

        if (stopMovingWhenVomiting) StopMoving();

        switch (mode)
        {
            case VomitMode.Barrage:
                int count = Mathf.Max(1, barrageCount);
                for (int i = 0; i < count; i++)
                {
                    float t = (count == 1) ? 0.5f : (float)i / (count - 1);
                    float angle = Mathf.Lerp(-barrageSpreadAngle * 0.5f, barrageSpreadAngle * 0.5f, t);
                    Vector2 d = Quaternion.Euler(0f, 0f, angle) * dir;
                    SpawnHairball(activePrefab, null, d, hairballSpeed, barrageRange, barrageLingerSeconds, 1f, barrageMass, havocBreaksObstacles, defaultGravityScale);
                }
                break;

            case VomitMode.Giant:
                SpawnHairball(activePrefab, null, dir, giantSpeed, hairballRange, giantLingerSeconds, giantScale, giantMass, giantBreaksObstacles, defaultGravityScale);
                break;

            default:
                SpawnHairball(activePrefab, null, dir, hairballSpeed, hairballRange, hairballLingerSeconds, 1f, normalMass, false, defaultGravityScale);
                break;
        }
        return true;
    }

    private GameObject GetActivePrefabForMode(VomitMode m)
    {
        switch (m)
        {
            case VomitMode.Skyward:
                if (skywardHairballPrefab != null) return skywardHairballPrefab;
                break;
            case VomitMode.Barrage:
                if (barrageHairballPrefab != null) return barrageHairballPrefab;
                break;
            case VomitMode.Giant:
                if (giantHairballPrefab != null) return giantHairballPrefab;
                break;
        }
        return hairballPrefab != null ? hairballPrefab.gameObject : null;
    }

    private void SpawnHairball(GameObject prefabToUse, Sprite customSprite, Vector2 dir, float speed, float range, float linger, float scale, float ballMass, bool canBreak = false, float gravScale = 1f)
    {
        if (prefabToUse == null) return;

        float forwardDist = mouthDistance;
        float heightOffset = 0f;

        if (scale > 1f)
        {
            // ชดเชยความสูงและระยะข้างหน้าสำหรับก้อนขนยักษ์ เพื่อไม่ให้เกิดมาแล้วจมพื้นหรือทับตัวแมว
            heightOffset = giantSpawnHeightOffset;
            forwardDist += giantSpawnForwardOffset;
        }

        Vector2 spawnOffset = dir * forwardDist + Vector2.up * heightOffset;
        float offsetDist = spawnOffset.magnitude;
        if (offsetDist > 0.001f)
        {
            Vector2 offsetDir = spawnOffset / offsetDist;
            RaycastHit2D hit = Physics2D.Raycast(rb.position, offsetDir, offsetDist + 0.1f);
            if (hit.collider != null && !hit.collider.isTrigger && hit.collider.GetComponentInParent<TestAnimal>() == null)
            {
                spawnOffset = offsetDir * Mathf.Max(0.05f, hit.distance - 0.15f);
            }
        }

        Vector2 spawnPos = rb.position + spawnOffset;
        GameObject go = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
        PlayAnimalSound(ResolveSfx(vomitSound, null, "CatVomit"));

        if (!go.TryGetComponent<Hairball>(out var ball))
        {
            ball = go.AddComponent<Hairball>();
        }

        if (customSprite != null)
        {
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sprite = customSprite;
        }

        if (!Mathf.Approximately(scale, 1f)) go.transform.localScale *= scale;
        ball.Mass = ballMass;
        ball.CanBreakObstacles = canBreak;
        ball.Init(dir, speed, range, linger, gravScale);
    }

    private Vector2 GetFacingDirectionForBubble()
    {
        Vector2 dir = FacingDirection;
        if (Mathf.Abs(dir.x) < 0.01f)
        {
            dir = ToVector(vomitDirection);
            if (Mathf.Abs(dir.x) < 0.01f)
            {
                dir = (spriteRenderer != null && spriteRenderer.flipX) ? Vector2.left : Vector2.right;
            }
        }
        return new Vector2(Mathf.Sign(dir.x != 0 ? dir.x : 1f), 0f);
    }

    private IEnumerator BubbleRoutine(Vector2 baseDir)
    {
        GameObject activePrefab = bubblePrefab != null ? bubblePrefab : hairballPrefab?.gameObject;
        if (activePrefab == null)
        {
            Debug.LogWarning($"[{animalName}] ยังไม่ได้ลาก Bubble Prefab หรือ Hairball Prefab ใส่ช่องใน Inspector");
            yield break;
        }

        FaceDirection(baseDir);

        int sets = Mathf.Max(1, bubbleBurstCount);
        int perSet = Mathf.Max(1, bubblesPerSet);

        for (int s = 0; s < sets; s++)
        {
            for (int b = 0; b < perSet; b++)
            {
                Vector2 shotDir = baseDir;
                if (bubbleSpreadAngle > 0.01f)
                {
                    float angle = Random.Range(-bubbleSpreadAngle * 0.5f, bubbleSpreadAngle * 0.5f);
                    shotDir = Quaternion.Euler(0f, 0f, angle) * baseDir;
                }

                SpawnSingleBubble(activePrefab, shotDir);

                if (b < perSet - 1 && delayBetweenBubbles > 0f)
                {
                    yield return new WaitForSeconds(delayBetweenBubbles);
                }
            }

            if (s < sets - 1 && delayBetweenBursts > 0f)
            {
                yield return new WaitForSeconds(delayBetweenBursts);
            }
        }
    }

    private void SpawnSingleBubble(GameObject prefabToUse, Vector2 dir)
    {
        Vector2 spawnOffset = dir * mouthDistance;
        Vector2 spawnPos = rb.position + spawnOffset;

        GameObject go = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
        PlayAnimalSound(ResolveSfx(bubbleSound, null, "CatBubble"));

        if (!go.TryGetComponent<Hairball>(out var ball))
        {
            ball = go.AddComponent<Hairball>();
        }

        if (bubbleSprite != null)
        {
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sprite = bubbleSprite;
        }

        ball.Mass = normalMass;
        ball.CanBreakObstacles = false;
        ball.SetupBubblePush(
            canPush: true,
            pushDist: bubblePushDistance,
            pushSpeed: bubblePushSpeed,
            destroyOnPush: bubblePopOnPush,
            popOnObstacle: bubblePopOnObstacle,
            popPrefab: bubblePopEffectPrefab
        );

        ball.Init(dir, bubbleSpeed, bubbleRange, bubbleLingerSeconds, bubbleGravityScale);
    }
    #endregion

    #region Skyward Bubble Float (ครอบฟองสบู่ใส่ตัวเองลอยไปมา เด้งกำแพงเหมือน Skyward วัวแต่ความเร็วไม่เพิ่ม)
    private void CreateBubbleShield()
    {
        RemoveBubbleShield();

        if (skywardBubblePrefab != null)
        {
            activeBubbleShield = Instantiate(skywardBubblePrefab, transform);
            activeBubbleShield.name = "SkywardBubbleShield";
            activeBubbleShield.transform.localPosition = (Vector3)skywardBubbleOffset;
            activeBubbleShield.transform.localRotation = Quaternion.identity;

            // ปิดการทำงานของฟิสิกส์/สคริปต์กระสุนที่อาจติดมากับ Prefab เพื่อไม่ให้ตีกับตัวแมว
            foreach (var childRb in activeBubbleShield.GetComponentsInChildren<Rigidbody2D>())
            {
                childRb.simulated = false;
            }
            foreach (var col in activeBubbleShield.GetComponentsInChildren<Collider2D>())
            {
                col.enabled = false;
            }
            var hb = activeBubbleShield.GetComponent<Hairball>();
            if (hb != null) Destroy(hb);

            // ตั้งค่า Sorting Order ให้อยู่ข้างหน้าตัวแมว
            if (spriteRenderer != null)
            {
                foreach (var sr in activeBubbleShield.GetComponentsInChildren<SpriteRenderer>())
                {
                    sr.sortingLayerID = spriteRenderer.sortingLayerID;
                    if (sr.sortingOrder <= spriteRenderer.sortingOrder)
                    {
                        sr.sortingOrder = spriteRenderer.sortingOrder + 1;
                    }
                }
            }

            Vector3 basePrefabScale = activeBubbleShield.transform.localScale;
            bubbleBaseScale = new Vector3(basePrefabScale.x * skywardBubbleShieldScale, basePrefabScale.y * skywardBubbleShieldScale, basePrefabScale.z);
            activeBubbleShield.transform.localScale = bubbleBaseScale;
        }
        else
        {
            activeBubbleShield = new GameObject("SkywardBubbleShield");
            activeBubbleShield.transform.SetParent(transform, false);
            activeBubbleShield.transform.localPosition = (Vector3)skywardBubbleOffset;

            bubbleShieldRenderer = activeBubbleShield.AddComponent<SpriteRenderer>();

            Sprite spriteToUse = bubbleSprite;
            if (spriteToUse == null && bubblePrefab != null)
            {
                var sr = bubblePrefab.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) spriteToUse = sr.sprite;
            }

            if (spriteToUse != null)
            {
                bubbleShieldRenderer.sprite = spriteToUse;
            }

            if (spriteRenderer != null)
            {
                bubbleShieldRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                bubbleShieldRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            }

            bubbleShieldRenderer.color = new Color(1f, 1f, 1f, skywardBubbleAlpha);
            bubbleBaseScale = Vector3.one * skywardBubbleShieldScale;
            activeBubbleShield.transform.localScale = bubbleBaseScale;
        }

        if (bodyCollider != null && skywardFlyOverLayers.value != 0)
        {
            bodyCollider.excludeLayers = skywardFlyOverLayers;
        }
    }

    private void UpdateBubbleShieldVisual()
    {
        if (activeBubbleShield == null || !skywardAnimateBubble) return;
        float wobble = 1f + Mathf.Sin(Time.time * 5f) * 0.05f;
        float wobbleY = 1f + Mathf.Cos(Time.time * 4f) * 0.05f;
        activeBubbleShield.transform.localScale = new Vector3(bubbleBaseScale.x * wobble, bubbleBaseScale.y * wobbleY, bubbleBaseScale.z);
    }

    private void RemoveBubbleShield()
    {
        if (bodyCollider != null)
        {
            bodyCollider.excludeLayers = 0;
        }

        if (activeBubbleShield != null)
        {
            if (bubblePopEffectPrefab != null)
            {
                Instantiate(bubblePopEffectPrefab, activeBubbleShield.transform.position, Quaternion.identity);
            }
            PlayAnimalSound(ResolveSfx(bubblePopSound, null, "CatBubblePop"));
            Destroy(activeBubbleShield);
            activeBubbleShield = null;
            bubbleShieldRenderer = null;
        }
    }

    private AudioClip ResolveSkywardSound()
    {
        if (skywardSound != null) return skywardSound;
        AudioClip clip = ResolveSfx(skywardSound, "Assets/Audioes/Animal/Cat/Bubble Skyward.mp3", "CatSkyward", "BubbleSkyward");
        if (clip != null) { skywardSound = clip; return skywardSound; }
#if UNITY_EDITOR
        string[] candidates = new[]
        {
            "Assets/Audioes/Animal/Cat/Bubble Skyward.mp3",
            "Assets/Audioes/Animal/Cat/bubble skyward.mp3",
            "Assets/Audioes/Animal/Cat/Cat bubble.mp3"
        };
        foreach (var p in candidates)
        {
            var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            if (loaded != null) { skywardSound = loaded; return skywardSound; }
        }
#endif
        return null;
    }

    private void PlaySkywardSound()
    {
        AudioClip clip = ResolveSkywardSound();
        if (clip != null)
        {
            float vol = (skywardVolume > 0.001f) ? skywardVolume : 1f;
            PlayAnimalSound(clip, vol);
        }
    }

    private AudioClip ResolveSkywardBounceSound()
    {
        if (skywardBounceSound != null) return skywardBounceSound;
        AudioClip clip = ResolveSfx(skywardBounceSound, "Assets/Audioes/Animal/Cow/Cow skyward.mp3", "CowBounce", "CowSkyward");
        if (clip != null) { skywardBounceSound = clip; return skywardBounceSound; }
#if UNITY_EDITOR
        string[] candidates = new[]
        {
            "Assets/Audioes/Animal/Cow/Cow skyward.mp3",
            "Assets/Audioes/Animal/Cow/cow skyward.mp3"
        };
        foreach (var p in candidates)
        {
            var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            if (loaded != null) { skywardBounceSound = loaded; return skywardBounceSound; }
        }
#endif
        return null;
    }

    private void PlaySkywardBounceSound()
    {
        AudioClip clip = ResolveSkywardBounceSound();
        if (clip != null)
        {
            float vol = (skywardBounceVolume > 0.001f) ? skywardBounceVolume : 1f;
            PlayAnimalSound(clip, vol, 0.05f, true);
        }
    }

    private IEnumerator SkywardBubbleRoutine()
    {
        if (isSkywardFloating) yield break;

        isSkywardFloating = true;
        skywardBounceCount = 0;
        UpdateGravityState();

        PlaySkywardSound();
        CreateBubbleShield();

        Vector2 launchDir;
        if (skywardDestination != null)
        {
            launchDir = ((Vector2)skywardDestination.position - rb.position).normalized;
            if (launchDir.sqrMagnitude < 0.001f) launchDir = Vector2.up;
        }
        else
        {
            float signX = (FacingDirection.x != 0) ? Mathf.Sign(FacingDirection.x) : 1f;
            float rad = skywardLaunchAngle * Mathf.Deg2Rad;
            launchDir = new Vector2(signX * Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        }

        currentBounceDir = launchDir;
        FacingDirection = launchDir;
        FaceDirection(launchDir);

        rb.linearVelocity = currentBounceDir * skywardBubbleSpeed + ConveyorVelocity;

        float startTime = Time.time;

        while (isSkywardFloating)
        {
            if (skywardMaxBounces > 0 && skywardBounceCount >= skywardMaxBounces)
            {
                Debug.Log($"[{animalName}] Skyward ฟองสบู่เด้งครบ {skywardMaxBounces} ครั้งแล้ว -> แตกและลงสู่พื้น");
                break;
            }

            if (skywardBubbleDuration > 0f && Time.time - startTime >= skywardBubbleDuration)
            {
                Debug.Log($"[{animalName}] Skyward ฟองสบู่หมดเวลา ({skywardBubbleDuration} วินาที) -> แตกและลงสู่พื้น");
                break;
            }

            if (skywardDestination != null && Vector2.Distance(rb.position, skywardDestination.position) <= 0.45f)
            {
                Debug.Log($"[{animalName}] Skyward ฟองสบู่ลอยถึงจุดเป้าหมาย {skywardDestination.name} แล้ว!");
                break;
            }

            yield return null;
        }

        EndSkywardBubble();
    }

    private void EndSkywardBubble()
    {
        if (!isSkywardFloating) return;

        isSkywardFloating = false;
        RemoveBubbleShield();
        StopMoving();
        rb.linearVelocity = Vector2.zero;
        UpdateGravityState();
        if (activeSkywardRoutine != null)
        {
            StopCoroutine(activeSkywardRoutine);
            activeSkywardRoutine = null;
        }
    }

    private void HandleSkywardBubblePhysics()
    {
        if (!isSkywardFloating) return;

        UpdateGravityState();

        float radius = 0.45f;
        if (bodyCollider is CircleCollider2D circle)
        {
            radius = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * 0.95f;
        }
        else if (bodyCollider != null)
        {
            radius = Mathf.Max(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y) * 0.95f;
        }

        float step = skywardBubbleSpeed * Time.fixedDeltaTime;
        float castDist = step + 0.08f;

        LayerMask mask = skywardBounceLayers.value != 0 ? skywardBounceLayers : ~skywardFlyOverLayers.value;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(rb.position, radius, currentBounceDir, castDist, mask);

        bool bounced = false;
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger || hit.collider.gameObject == gameObject) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;

            PushableBox box = hit.collider.GetComponentInParent<PushableBox>();
            if (box != null && box.IsBroken) continue;

            Door door = hit.collider.GetComponentInParent<Door>();
            if (door != null && (door.IsBroken || door.IsOpen)) continue;

            OnSkywardBounce(hit.normal, box);
            bounced = true;
            break;
        }

        if (!bounced)
        {
            // ความเร็วคงที่ ไม่เพิ่มขึ้นตามที่ผู้เล่นต้องการ
            rb.linearVelocity = currentBounceDir * skywardBubbleSpeed + ConveyorVelocity;
        }
    }

    private void OnSkywardBounce(Vector2 normal, PushableBox hitBox = null)
    {
        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = -currentBounceDir;
        }
        normal.Normalize();

        if (Vector2.Dot(currentBounceDir, normal) < 0f)
        {
            currentBounceDir = Vector2.Reflect(currentBounceDir, normal).normalized;

            if (Mathf.Abs(currentBounceDir.x) < 0.15f)
            {
                float sign = currentBounceDir.x != 0 ? Mathf.Sign(currentBounceDir.x) : (Random.value < 0.5f ? -1f : 1f);
                currentBounceDir.x = sign * 0.25f;
                currentBounceDir.Normalize();
            }
            if (Mathf.Abs(currentBounceDir.y) < 0.15f)
            {
                float sign = currentBounceDir.y != 0 ? Mathf.Sign(currentBounceDir.y) : (Random.value < 0.5f ? -1f : 1f);
                currentBounceDir.y = sign * 0.25f;
                currentBounceDir.Normalize();
            }

            FacingDirection = currentBounceDir;
            FaceDirection(currentBounceDir);

            PlaySkywardBounceSound();

            rb.position += normal * 0.05f;

            // ความเร็วคงที่ ไม่เพิ่มขึ้น
            rb.linearVelocity = currentBounceDir * skywardBubbleSpeed + ConveyorVelocity;

            skywardBounceCount++;
            Debug.Log($"[{animalName}] Skyward ฟองสบู่เด้งครั้งที่ {skywardBounceCount}/{(skywardMaxBounces > 0 ? skywardMaxBounces.ToString() : "ไม่จำกัด")}");

            if (skywardPushBoxesOnBounce && hitBox != null && !hitBox.IsBroken)
            {
                Vector2 pushDir = new Vector2(Mathf.Sign(-normal.x != 0 ? -normal.x : currentBounceDir.x), 0f);
                if (Mathf.Abs(pushDir.x) > 0.01f)
                {
                    hitBox.TryPush(pushDir, skywardBoxPushDistance, bubblePushSpeed);
                }
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isSkywardFloating)
        {
            HandleSkywardCollision(collision);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isSkywardFloating)
        {
            HandleSkywardCollision(collision);
        }
    }

    private void HandleSkywardCollision(Collision2D collision)
    {
        if (!isSkywardFloating) return;

        Vector2 normal = Vector2.zero;
        int count = collision.contactCount;
        if (count > 0)
        {
            for (int i = 0; i < count; i++) normal += collision.contacts[i].normal;
            normal.Normalize();
        }
        if (normal.sqrMagnitude > 0.01f)
        {
            PushableBox box = collision.gameObject.GetComponentInParent<PushableBox>();
            OnSkywardBounce(normal, box);
        }
    }
    #endregion
}