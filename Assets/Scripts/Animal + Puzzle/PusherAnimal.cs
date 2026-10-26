using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// สัตว์ทดลองตัวที่ 3: ตัวผลัก/ดันกล่อง (ใช้ยาเพิ่มพละกำลัง)
/// ความสามารถ: ดันกล่อง (PushableBox) ที่อยู่ใกล้และอยู่ทางเดียวกับทิศที่เล็ง ให้เลื่อนไปตามทิศนั้น
/// กล่องจะหยุดเองเมื่อชนกำแพง/ประตู/กล่องอื่น ใช้ดันกล่องไปทับสวิตช์ได้
/// โหมดอัตโนมัติ: ได้ยาแล้วหากล่องที่ใกล้ที่สุด เดินไปยืนด้านหลัง แล้วผลักไปทิศที่ตั้งไว้เอง
/// </summary>
public class PusherAnimal : TestAnimal
{
    #region Push Settings
    [Header("Push (ดันกล่อง)")]
    [Tooltip("Layer ของกล่องที่ดันได้")]
    [SerializeField] private LayerMask boxLayer;

    [Tooltip("ระยะที่เอื้อมถึงกล่อง (นับจากกลางตัว)")]
    [SerializeField] private float pushReach = 1.3f;

    [Tooltip("ระยะที่ดันกล่องให้เลื่อนไปต่อ 1 ครั้ง")]
    [SerializeField] private float pushDistance = 1.5f;

    [Tooltip("ความเร็วที่กล่องเลื่อน")]
    [SerializeField] private float pushSpeed = 6f;

    [Tooltip("ติ๊กไว้ = ดันได้แค่ 4 ทิศตรง (บน/ล่าง/ซ้าย/ขวา) เหมาะกับปริศนากล่อง / ไม่ติ๊ก = ดันตามทิศเป๊ะๆ")]
    [SerializeField] private bool snapToCardinalDirections = true;

    [Tooltip("ทิศกล่องต้องตรงกับทิศที่เล็งแค่ไหนถึงจะดัน (1 = ตรงเป๊ะ, 0 = ขอแค่อยู่ครึ่งวงกลมข้างหน้า)")]
    [SerializeField, Range(0f, 1f)] private float aimAlignment = 0.5f;

    [Tooltip("เสียงเอฟเฟกต์ตอนแพะออกแรงผลักกล่อง")]
    [SerializeField] private AudioClip pushSound;

    [Range(0f, 1f)]
    [Tooltip("ระดับความดังของเสียงตอนผลัก (0 - 1)")]
    [SerializeField] private float pushVolume = 1f;

    [Tooltip("เสียงเอฟเฟกต์ตอนแพะทุบ/ชนทำลายกล่อง")]
    [SerializeField] private AudioClip smashSound;

    [Range(0f, 1f)]
    [Tooltip("ระดับความดังของเสียงตอนทุบ (0 - 1)")]
    [SerializeField] private float smashVolume = 1f;
    #endregion

    #region Combo Settings
    private enum GoatMode { Push, ForwardLaser, SkywardLaser, Havoc, Ceiling }
    private enum CeilingState { Floor, Rising, OnCeiling, Falling }

    [Header("Green Potion (ยาเขียว: ยิงเลเซอร์ออกข้างหน้า)")]
    [Tooltip("Prefab สำหรับลำแสงเลเซอร์ยิงตรงไปข้างหน้า (ถ้าเว้นว่างจะวาดด้วยโค้ด)")]
    [SerializeField] private GameObject laserPrefab;

    [Tooltip("จุดที่ลำแสงเลเซอร์พุ่งออกมา (เช่น ปาก/เขา/ตาแพะ) ถ้าเว้นว่างจะยิงจากกลางตัว")]
    [SerializeField] private Transform laserFirePoint;

    [Tooltip("Prefab ประกายไฟ/เอฟเฟกต์ระเบิดตรงจุดที่เลเซอร์ชนโดน (Hit Impact)")]
    [SerializeField] private GameObject laserHitPrefab;

    [Tooltip("Prefab ประกายไฟตรงจุดยิง (Muzzle Flash)")]
    [SerializeField] private GameObject laserMuzzlePrefab;

    [Tooltip("ระยะยิงเลเซอร์ออกข้างหน้า (ทำลายกล่องที่พังได้ ดันกล่องที่พังไม่ได้ กดปุ่ม/สวิตช์ 1 ครั้งต่อนัด)")]
    [SerializeField] private float laserRange = 30f;

    [Tooltip("ให้เลเซอร์ยิงออกข้างหน้ากดปุ่ม/สวิตช์ (FloorSwitch) ได้หรือไม่ (ถ้าไม่ติ๊ก เลเซอร์จะไม่กดปุ่ม/สวิตช์)")]
    [SerializeField] private bool laserCanToggleSwitches = true;

    [Tooltip("ให้เลเซอร์เปิด/ปิดประตูธรรมดาได้หรือไม่ (ปกติ false: ประตูธรรมดาต้องเปิดด้วยสวิตช์เท่านั้น ไม่เปิดด้วยเลเซอร์)")]
    [SerializeField] private bool laserCanToggleDoors = false;

    [Tooltip("Layer ที่ทำให้เลเซอร์หยุดทันที (เช่น กำแพง พื้น ประตู กล่อง) เว้นว่าง = ทะลุทุกอย่าง")]
    [SerializeField] private LayerMask laserBlockLayers;

    [Tooltip("สีของเลเซอร์ (ใช้กรณีวาดด้วยโค้ดหรือย้อมสี)")]
    [SerializeField] private Color laserColor = new Color(0.4f, 0.9f, 1f);

    [Tooltip("ความหนาของลำแสงเลเซอร์ (กรณีวาดด้วยโค้ด)")]
    [SerializeField] private float laserWidth = 0.18f;

    [Tooltip("เลเซอร์ค้างให้เห็นกี่วินาที")]
    [SerializeField] private float laserDisplaySeconds = 0.3f;

    [Tooltip("เสียงเอฟเฟกต์ตอนยิงเลเซอร์ (Optional)")]
    [SerializeField] private AudioClip laserSound;

    [Tooltip("ให้ยาเขียว (Vomit) ทำให้แพะยิงเลเซอร์ออกข้างหน้า")]
    [SerializeField] private bool allowGreenPotionLaser = true;

    [Header("Skyward Potion (ยา Skyward เขียว+ฟ้า: เสกเลเซอร์ลงมาจากฟ้า)")]
    [Tooltip("Prefab สำหรับลำแสงเลเซอร์ที่เสกลงมาจากฟ้า (ถ้าเว้นว่างจะค้นหา Laser Skyward ให้อัตโนมัติ หรือวาดเส้นด้วยโค้ด)")]
    [SerializeField] private GameObject skywardLaserPrefab;

    [Tooltip("ความสูงของจุดเริ่มลำแสงเลเซอร์บนท้องฟ้าเหนือเป้าหมาย")]
    [SerializeField] private float skyLaserHeight = 20f;

    [Tooltip("Prefab ประกายไฟ/ระเบิดตรงจุดตกของเลเซอร์จากฟ้า (Impact)")]
    [SerializeField] private GameObject skywardHitPrefab;

    [Tooltip("เสียงเอฟเฟกต์ตอนเลเซอร์ผ่าลงมาจากฟ้า (Optional)")]
    [SerializeField] private AudioClip skywardSound;

    [Tooltip("สีของเลเซอร์จากฟ้า (กรณีวาดด้วยโค้ด)")]
    [SerializeField] private Color skywardLaserColor = new Color(0.3f, 1f, 0.85f, 1f);

    [Tooltip("ความหนาของลำแสงเลเซอร์จากฟ้า (กรณีวาดด้วยโค้ด)")]
    [SerializeField] private float skywardLaserWidth = 0.25f;

    [Tooltip("เลเซอร์จากฟ้าค้างให้เห็นกี่วินาที")]
    [SerializeField] private float skywardDisplaySeconds = 0.35f;

    [Header("Skyward Search Range (ระยะค้นหาเป้าหมายเลเซอร์ฟ้า)")]
    [FormerlySerializedAs("skywardAutoSearchRadius")]
    [Tooltip("ระยะค้นหาแนวนอนไปข้างหน้า (แกน X ตามทิศที่แพะหัน)")]
    [SerializeField] private float skywardHorizontalRange = 25f;

    [Tooltip("ระยะค้นหาแนวตั้ง (แกน Y สูง-ต่ำ ± จากตัวแพะ เช่น 10 = ขึ้นได้ 10 ลงได้ 10 หน่วย)")]
    [SerializeField] private float skywardVerticalRange = 10f;

    [Tooltip("ความสูงจุดกึ่งกลางของกรอบค้นหาแนวตั้ง (Offset แกน Y จากตัวแพะ เช่น 0 = กึ่งกลางตัวแพะพอดี)")]
    [SerializeField] private float skywardVerticalOffset = 0f;

    [Tooltip("ให้เลเซอร์จากฟ้าค้นหาเป้าหมายเฉพาะด้านหน้าที่แพะหันไปเท่านั้น (ไม่เล็งย้อนกลับหลัง)")]
    [SerializeField] private bool skywardAimFrontOnly = true;

    [Tooltip("จำนวนเป้าหมายที่อยู่ใกล้ที่สุดที่จะผ่าเลเซอร์ลงมาใส่พร้อมกัน (1 = ตีตัวที่ใกล้สุด 1 ชิ้น, 2 = ตี 2 ชิ้นที่ใกล้สุด ...)")]
    [Min(1)]
    [SerializeField] private int skywardTargetCount = 1;

    [Tooltip("ระยะห่างเวลาระหว่างเลเซอร์แต่ละเส้นเมื่อตีหลายเป้าหมาย (วินาที, 0 = ผ่าลงมาพร้อมกันทุกเส้น)")]
    [SerializeField] private float skywardMultiStrikeInterval = 0.08f;

    [Tooltip("ให้เลเซอร์จากฟ้ากดปุ่ม/สวิตช์ (FloorSwitch) ได้หรือไม่ (ถ้าไม่ติ๊ก เลเซอร์จะไม่กดสวิตช์และไม่ล็อคเป้าหมายที่สวิตช์)")]
    [SerializeField] private bool skywardCanToggleSwitches = true;

    [Header("Skyward Gizmos (การแสดงผลกรอบค้นหาใน Scene View)")]
    [Tooltip("แสดงกรอบค้นหาของเลเซอร์ฟ้า (Skyward) ตอนคลิกเลือกแพะใน Scene View")]
    [SerializeField] private bool showSkywardGizmo = true;

    [Tooltip("แสดงกรอบค้นหาของเลเซอร์ฟ้าตลอดเวลา แม้ไม่ได้คลิกเลือกแพะ")]
    [SerializeField] private bool showSkywardGizmoAlways = false;

    [Tooltip("สีของกรอบ Gizmo เลเซอร์ฟ้า")]
    [SerializeField] private Color skywardGizmoColor = new Color(0f, 0.8f, 1f, 0.4f);

    [Header("Skyward Piercing & Blocking (การทะลุ / สิ่งที่ขวางเลเซอร์ฟ้า - ปรับแยกอิสระ)")]
    [Tooltip("ทะลุกำแพง/เพดาน/สิ่งกีดขวาง (เลเซอร์ผ่าลงมาจากฟ้าทะลุหลังคาลงมาหาเป้าหมายได้ ไม่ติดกำแพงด้านบน)")]
    [SerializeField] private bool skywardPierceWalls = true;

    [Tooltip("Layer ที่ทำให้เลเซอร์จากฟ้าหยุดทันที (เฉพาะเลเซอร์จากฟ้า ไม่เกี่ยวอะไรกับ Laser Block Layers) ถ้าเว้นว่าง = ไม่มี Layer ไหนขวาง")]
    [SerializeField] private LayerMask skywardBlockLayers;

    [Tooltip("ให้เลเซอร์จากฟ้าเปิด/ปิดประตูธรรมดาได้หรือไม่ (ปกติ false: ประตูธรรมดาเปิดด้วยสวิตช์เท่านั้น)")]
    [SerializeField] private bool skywardCanToggleDoors = false;

    [Tooltip("ทะลุผ่านกล่องที่พังได้ (หลังทำลายกล่องแล้ว ลำแสงทะลุลงไปโดนสิ่งที่อยู่ข้างใต้ต่อได้)")]
    [SerializeField] private bool skywardPierceBreakableBoxes = true;

    [Tooltip("ทะลุผ่านกล่องที่พังไม่ได้ (ถ้าไม่ติ๊ก เลเซอร์จะหยุดบนหัวกล่อง)")]
    [SerializeField] private bool skywardPierceSolidBoxes = false;

    [Tooltip("ทะลุผ่านสวิตช์/ปุ่ม (กดสวิตช์แล้วลำแสงทะลุลงไปต่อได้)")]
    [SerializeField] private bool skywardPierceSwitches = true;

    [Tooltip("ทะลุผ่านประตูที่พังแล้ว หรือประตูที่เปิดอยู่")]
    [SerializeField] private bool skywardPierceOpenDoors = true;

    [Tooltip("ประตูธรรมดาที่ปิดอยู่ขวางเลเซอร์หรือไม่ (ถ้าติ๊ก = เลเซอร์หยุดที่ประตู / ถ้าไม่ติ๊ก = ทะลุผ่าน)")]
    [SerializeField] private bool skywardBlockedByClosedDoors = true;

    [Tooltip("จำนวนวัตถุสูงสุดที่เลเซอร์จากฟ้าทะลุผ่านได้ (99 = ไม่จำกัด)")]
    [SerializeField] private int skywardMaxPierceCount = 99;

    [Header("Havoc (ส้ม+เขียว): ถล่มทุกอย่างที่พังได้ทั้งฉาก")]
    [Tooltip("เสียงเอฟเฟกต์ตอนแพะใช้พลัง Havoc ถล่มทุกอย่าง (ถ้าเว้นว่างจะค้นหาจาก AudioManager: 'GoatHavoc')")]
    [SerializeField] private AudioClip havocSound;

    [Range(0f, 1f)]
    [Tooltip("ระดับความดังของเสียง Havoc (0 - 1)")]
    [SerializeField] private float havocVolume = 1f;

    [Tooltip("สไปรต์ร่างแพะตอนได้รับยา Havoc (เว้นว่าง = ใช้ Powered Up Sprite ปกติ)")]
    [SerializeField] private Sprite havocFormSprite;

    [Header("Surge (ฟ้า+ส้ม): ยากราวิตี้ กระโดดแตะเพดานแล้วเดินบนเพดาน")]
    [Tooltip("Layer ของพื้น/เพดาน ใช้หาเพดานที่จะกระโดดไปแตะ และหาพื้นตอนกลับลง เว้นว่าง = ทุก Layer ที่ไม่ใช่ Trigger")]
    [SerializeField] private LayerMask ceilingLayers;

    [Tooltip("ค้นหาเพดาน/พื้นไกลสุดกี่หน่วย")]
    [SerializeField] private float ceilingSearchDistance = 20f;

    [Tooltip("ความเร็วตอนกระโดดขึ้นไปแตะเพดาน / ตกกลับลงพื้น")]
    [SerializeField] private float ceilingRiseSpeed = 14f;

    [Tooltip("ตัวคูณความเร็วเดินตอนอยู่บนเพดาน")]
    [SerializeField] private float ceilingWalkSpeedMultiplier = 1.2f;

    [Tooltip("อยู่บนเพดานนานกี่วินาทีแล้วตกลงเอง (0 = อยู่ได้ไม่จำกัด กดใช้ความสามารถซ้ำเพื่อลง)")]
    [SerializeField] private float ceilingDuration = 10f;

    [Tooltip("Layer ที่ทะลุผ่านได้ตอนเดินกลับหัวบนเพดาน (เช่น กล่องบนพื้น) กำแพงแนวตั้งยังขวางปกติ")]
    [SerializeField] private LayerMask ceilingPassLayers;

    [Tooltip("โหมดอัตโนมัติ: ระยะที่เดินบนเพดานไปทางซ้าย/ขวาตามทิศที่ตั้งไว้ แล้วตกลง")]
    [SerializeField] private float ceilingAutoWalkDistance = 6f;

    private GoatMode comboMode = GoatMode.Push;
    private CeilingState ceilingState = CeilingState.Floor;
    private float ceilingY;
    private float groundYBeforeRise;
    private float ceilingWalkDir = 1f;
    private float bounceCooldown = 0f;
    private Collider2D bodyCollider;

    public override bool IsAirborne => ceilingState != CeilingState.Floor;
    public override bool CanBreakObstacles => CanSmash;
    protected override float SpeedMultiplier => ceilingState == CeilingState.OnCeiling ? ceilingWalkSpeedMultiplier : 1f;
    #endregion

    #region Auto
    [Header("Auto Push (โหมดอัตโนมัติ)")]
    [Tooltip("ทิศที่จะผลักกล่องไปเมื่อได้ยา")]
    [SerializeField] private AutoDirection pushDirection = AutoDirection.Right;

    [Tooltip("รัศมีที่ใช้หากล่องที่ใกล้ที่สุด (นับจากตัวสัตว์)")]
    [SerializeField] private float autoSearchRadius = 6f;

    [Tooltip("จำนวนครั้งที่ผลักต่อกัน (สัตว์จะเดินตามกล่องไปผลักต่อ)")]
    [SerializeField] private int autoPushCount = 1;

    [Tooltip("ระยะที่ไปยืนห่างจากกลางกล่องด้านหลัง (ควรน้อยกว่า Push Reach)")]
    [SerializeField] private float standOffset = 1f;

    protected override IEnumerator AutoRoutine()
    {
        Vector2 dir = ToVector(pushDirection);
        if (comboMode != GoatMode.Push)
        {
            // ยาเขียว (เลเซอร์หน้า) / ยา Skyward (เลเซอร์ฟ้า): เล็งไปด้านหน้าที่แพะหันหน้าอยู่จริง
            if (comboMode == GoatMode.ForwardLaser || comboMode == GoatMode.SkywardLaser)
                dir = GetFacingVector();

            yield return ComboAutoRoutine(dir);
            yield break;
        }
        if (snapToCardinalDirections)
        {
            dir = (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                ? new Vector2(Mathf.Sign(dir.x), 0f)
                : new Vector2(0f, Mathf.Sign(dir.y));
        }

        PushableBox box = FindNearestBox();
        if (box == null)
        {
            Debug.LogWarning($"[{animalName}] ไม่เจอกล่องในรัศมี {autoSearchRadius}");
            yield break;
        }

        Vector2 toBox = (Vector2)box.transform.position - rb.position;
        // ตรวจสอบว่ากล่องอยู่ทิศไหนเทียบกับตัวสัตว์:
        // ถ้ากล่องอยู่ข้างหน้า และทิศ dir ที่ตั้งไว้จะทำให้สัตว์ต้องเดินทะลุกล่องไปยืนอีกฝั่ง (เช่น สัตว์อยู่ซ้าย กล่องอยู่กลาง แต่ dir = Left)
        // สัตว์จะไม่สามารถเดินผ่านกล่องที่ตันได้ ให้กลับทิศ dir ผลักออกจากตัวสัตว์ไปข้างหน้าแทน
        if (Mathf.Abs(dir.x) > 0.01f && Mathf.Abs(toBox.x) > 0.05f)
        {
            bool crossingBoxNeeded = Mathf.Sign(toBox.x) != Mathf.Sign(dir.x);
            if (crossingBoxNeeded)
            {
                dir = new Vector2(Mathf.Sign(toBox.x), 0f);
            }
        }

        for (int i = 0; i < autoPushCount; i++)
        {
            if (i > 0) yield return new WaitForSeconds(abilityCooldown);

            // เดินไปยืนด้านหลังกล่อง (ฝั่งตรงข้ามทิศที่จะผลัก) โดยคุมระดับ Y บนพื้นของสัตว์เอง
            Vector2 standPos = new Vector2(box.transform.position.x - dir.x * standOffset, rb.position.y);
            MoveTo(standPos);
            while (IsMoving) yield return null;

            // หันหน้าไปยังทิศที่จะผลัก
            FaceDirection(dir);

            // ผลัก
            if (!ExecuteAbility(rb.position + dir)) yield break; // ผลักไม่ไปแล้ว (ติดกำแพง) เลิก

            while (box != null && (box.IsMoving || box.IsProcessingHit)) yield return null;
        }
    }

    private LayerMask GetEffectiveBoxLayer()
    {
        if (boxLayer.value != 0) return boxLayer;
        int mask = LayerMask.GetMask("Box");
        return mask != 0 ? (LayerMask)mask : (LayerMask)(~0);
    }

    private PushableBox FindNearestBox()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, autoSearchRadius, GetEffectiveBoxLayer());
        PushableBox best = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            PushableBox b = hit.GetComponentInParent<PushableBox>();
            if (b == null) continue;
            float d = Vector2.Distance(rb.position, b.transform.position);
            if (d < bestDist) { bestDist = d; best = b; }
        }
        return best;
    }
    #endregion

    #region Potion
    [Header("Potion Modes (ยาไหน = โหมดไหน)")]
    [Tooltip("ยาที่ทำให้เป็นโหมด 'ดันกล่อง' (แพะโย้ง)")]
    [SerializeField] private PotionType pushPotion = PotionType.Strength;

    [Tooltip("ยาที่ทำให้เป็นโหมด 'ทำลายกล่อง' (แพะกล้าม) เว้น None = ไม่ใช้ระบบ 2 โหมด (ตัวเดียวทำทั้งดันและทำลายตามกล่อง)")]
    [SerializeField] private PotionType smashPotion = PotionType.None;

    [Tooltip("(ไม่บังคับ) สไปรต์ตอนเป็นแพะกล้าม (โหมดทำลาย)")]
    [SerializeField] private Sprite smashFormSprite;

    [Tooltip("(ไม่บังคับ) สไปรต์ตอนเป็นแพะโย้ง (โหมดดัน)")]
    [SerializeField] private Sprite pushFormSprite;

    private bool smashMode = false;

    private bool IsDualMode => smashPotion != PotionType.None;

    /// <summary>ตอนนี้ชนแล้วทำลายกล่องได้ไหม (โหมด 2 ยา: ได้เฉพาะตอนกินยาทำลาย)</summary>
    public bool CanSmash => !IsDualMode || smashMode;

    public bool LaserCanToggleSwitches
    {
        get => laserCanToggleSwitches;
        set => laserCanToggleSwitches = value;
    }

    public bool SkywardCanToggleSwitches
    {
        get => skywardCanToggleSwitches;
        set => skywardCanToggleSwitches = value;
    }

    protected override PotionType RequiredPotion => pushPotion;

    public override bool AcceptsPotion(PotionType potion)
    {
        if (potion == PotionType.None) return false;
        if (allowGreenPotionLaser && potion == PotionType.Vomit) return true;
        return potion == pushPotion || (IsDualMode && potion == smashPotion) || IsComboPotion(potion);
    }

    protected override void OnPotionDrunk(PotionType potion)
    {
        switch (potion)
        {
            case PotionType.Vomit:
                // ยาเขียว: ยิงเลเซอร์ออกข้างหน้า
                comboMode = GoatMode.ForwardLaser;
                break;
            case PotionType.Skyward:
                // ยา Skyward (เขียว+ฟ้า): เสกเลเซอร์ลงมาจากฟ้า
                comboMode = GoatMode.SkywardLaser;
                break;
            case PotionType.Havoc: comboMode = GoatMode.Havoc; break;
            case PotionType.Surge: comboMode = GoatMode.Ceiling; break;
            default: comboMode = GoatMode.Push; break;
        }

        smashMode = IsDualMode && potion == smashPotion;

        Sprite s = (potion == PotionType.Havoc && havocFormSprite != null)
            ? havocFormSprite
            : (smashMode ? smashFormSprite : pushFormSprite);
        if (s != null && spriteRenderer != null) spriteRenderer.sprite = s;

        Debug.Log($"[{animalName}] โหมด: {(smashMode ? "ทำลายกล่อง" : (potion == PotionType.Havoc ? "Havoc" : "ดันกล่อง"))}");
    }

    protected override Sprite GetCustomPoweredUpSprite(PotionType potion)
    {
        if (potion == PotionType.Havoc && havocFormSprite != null) return havocFormSprite;
        return smashMode ? smashFormSprite : pushFormSprite;
    }
    #endregion

    #region Ability
    protected override bool PerformAbility(Vector2 aimWorldPoint)
    {
        Vector2 dir = aimWorldPoint - rb.position;
        if (dir.sqrMagnitude < 0.0001f) dir = FacingDirection;

        if (comboMode != GoatMode.Push) return PerformComboAbility(aimWorldPoint, dir.normalized);

        if (snapToCardinalDirections)
        {
            dir = (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                ? new Vector2(Mathf.Sign(dir.x), 0f)
                : new Vector2(0f, Mathf.Sign(dir.y));
        }
        dir.Normalize();
        FacingDirection = dir;
        FaceDirection(dir); // ผลักไปทางไหนก็หันไปทางนั้น (ไม่ใช่ทางที่เดินมาตอนไปยืนหลังกล่อง)

        PushableBox box = FindBoxInFront(dir);
        if (box == null) return false; // ไม่มีกล่องให้ดัน ไม่เริ่ม cooldown

        // โหมดทำลาย (แพะกล้าม): ทำลายได้เฉพาะกล่องที่ตั้งให้พังได้ ไม่ต้องอยู่ติดกำแพง
        if (IsDualMode && smashMode)
        {
            if (!box.IsDestructible || !box.CanAnimalDestroyBox(this)) return false;
            StopMoving();
            AudioClip smashClip = ResolveSmashSound();
            float vol = (smashVolume > 0.001f) ? smashVolume : 1f;
            PlayAnimalSound(smashClip, vol);
            box.TriggerDestructionSequence(this, dir);
            return true;
        }

        StopMoving();
        AudioClip clip = ResolvePushSound();
        float pVol = (pushVolume > 0.001f) ? pushVolume : 1f;
        PlayAnimalSound(clip, pVol);
        return box.TryPush(dir, pushDistance, pushSpeed, this);
    }

    private AudioClip ResolvePushSound()
    {
        if (pushSound != null) return pushSound;
        if (AudioManager.Instance != null)
        {
            AudioClip clip = AudioManager.Instance.GetClip("GoatPush")
                          ?? AudioManager.Instance.GetClip("Goat")
                          ?? AudioManager.Instance.GetClip("PusherAnimalPush");
            if (clip != null) { pushSound = clip; return pushSound; }
        }
#if UNITY_EDITOR
        pushSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audioes/Animal/Goat/Goat orange V2.mp3");
        if (pushSound != null) return pushSound;

        string[] guids = UnityEditor.AssetDatabase.FindAssets("Goat orange V2 t:AudioClip");
        if (guids == null || guids.Length == 0) guids = UnityEditor.AssetDatabase.FindAssets("Goat Orange t:AudioClip");
        if (guids != null && guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            pushSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            return pushSound;
        }
#endif
        return null;
    }

    private AudioClip ResolveSmashSound()
    {
        if (smashSound != null) return smashSound;
        if (AudioManager.Instance != null)
        {
            AudioClip clip = AudioManager.Instance.GetClip("GoatSmash")
                          ?? AudioManager.Instance.GetClip("GoatPush")
                          ?? AudioManager.Instance.GetClip("Goat");
            if (clip != null) return clip;
        }
        return null;
    }

    private PushableBox FindBoxInFront(Vector2 dir)
    {
        float reach = Mathf.Max(pushReach, standOffset + 0.6f);
        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, reach, GetEffectiveBoxLayer());
        PushableBox best = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            PushableBox box = hit.GetComponentInParent<PushableBox>();
            if (box == null) continue;

            Vector2 toBox = (Vector2)box.transform.position - rb.position;
            // ถ้าเป็นการผลักแนวนอน ให้เช็คความตรงของทิศแนวนอน เพื่อไม่ให้ความต่างของแกน Y เล็กน้อยทำให้องศาเพี้ยน
            if (Mathf.Abs(dir.x) > 0.5f)
            {
                if (Mathf.Sign(toBox.x) != Mathf.Sign(dir.x)) continue;
            }
            else if (Vector2.Dot(toBox.normalized, dir) < aimAlignment)
            {
                continue;
            }

            float dist = toBox.magnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = box;
            }
        }
        return best;
    }
    #endregion

    #region Combo Abilities
    protected override void Awake()
    {
        base.Awake();
        bodyCollider = GetComponent<Collider2D>();
        EnsureSkywardLaserPrefab();
        ResolveHavocSound();
        ResolvePushSound();
        ResolveLaserSound();
        ResolveSkywardSound();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (havocSound == null) ResolveHavocSound();
        if (pushSound == null) ResolvePushSound();
        if (laserSound == null) ResolveLaserSound();
        if (skywardSound == null) ResolveSkywardSound();
    }
#endif

    protected override void Start()
    {
        base.Start();
        FacingDirection = GetFacingVector();
    }

    /// <summary>
    /// หาเวกเตอร์ทิศทางด้านหน้าที่แพะกำลังหันไปในโลก (ซ้าย หรือ ขวา)
    /// </summary>
    public Vector2 GetFacingVector()
    {
        // 1. ถ้ามี SpriteRenderer ให้ดูทิศที่สไปรต์กำลังหันจริงในโลก
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            float axisX = transform.right.x * Mathf.Sign(transform.lossyScale.x);
            bool mirrored = axisX < -0.01f;
            bool artFacesWorldRight = spriteFacesRight != mirrored;
            bool isFacingLeft = artFacesWorldRight ? spriteRenderer.flipX : !spriteRenderer.flipX;
            return isFacingLeft ? Vector2.left : Vector2.right;
        }

        // 2. ถ้ามี FacingDirection
        if (Mathf.Abs(FacingDirection.x) > 0.01f)
        {
            return (FacingDirection.x < 0f) ? Vector2.left : Vector2.right;
        }

        // 3. ถ้ามีการหมุน/สเกล Transform
        float rightX = transform.right.x * Mathf.Sign(transform.lossyScale.x);
        if (Mathf.Abs(rightX) > 0.01f)
        {
            return (rightX < 0f) ? Vector2.left : Vector2.right;
        }

        // 4. ค่าทิศจาก pushDirection ที่ตั้งไว้
        Vector2 pd = ToVector(pushDirection);
        if (Mathf.Abs(pd.x) > 0.01f)
        {
            return (pd.x < 0f) ? Vector2.left : Vector2.right;
        }

        return Vector2.right;
    }

    protected override void Update()
    {
        base.Update();

        // เมื่ออยู่บนเพดาน: ตัวกลับหัว และหันซ้ายขวาตามทิศที่กำลังเดิน
        if (ceilingState == CeilingState.OnCeiling && spriteRenderer != null)
        {
            spriteRenderer.flipY = true;
            FaceHorizontal(ceilingWalkDir < 0f);
        }
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        // เดินบนเพดาน: ล็อกแกน Y ติดเพดานตลอดไป และเดินไป-กลับเมื่อชนซ้ายขวา
        if (ceilingState == CeilingState.OnCeiling)
        {
            if (bounceCooldown > 0f) bounceCooldown -= Time.fixedDeltaTime;

            // ตรวจจับสิ่งกีดขวางข้างหน้า (ซ้ายหรือขวา)
            if (bounceCooldown <= 0f && CheckHorizontalObstacle(ceilingWalkDir))
            {
                ceilingWalkDir = -ceilingWalkDir;
                bounceCooldown = 0.2f;
            }

            float speed = moveSpeed * SpeedMultiplier;
            rb.linearVelocity = new Vector2(ceilingWalkDir * speed, 0f) + ConveyorVelocity;
            rb.position = new Vector2(rb.position.x, ceilingY);

            FacingDirection = new Vector2(ceilingWalkDir, 0f);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (ceilingState == CeilingState.OnCeiling)
        {
            HandleCeilingBounce(collision);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (ceilingState == CeilingState.OnCeiling)
        {
            HandleCeilingBounce(collision);
        }
    }

    /// <summary>สลับทิศเดินเฉพาะเมื่อไปชนสิ่งกีดขวางแนวตั้ง (ซ้ายหรือขวาเท่านั้น ชนเพดานด้านบนไม่สลับ)</summary>
    private void HandleCeilingBounce(Collision2D collision)
    {
        if (bounceCooldown > 0f) return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;

            // ตรวจว่าเป็นการชนแนวตั้งซ้าย-ขวาเท่านั้น (|normal.x| มาก และไม่ใช่พื้น/เพดาน |normal.y| น้อย)
            if (normal.x < -0.4f && Mathf.Abs(normal.y) < 0.7f && ceilingWalkDir > 0f)
            {
                ceilingWalkDir = -1f;
                bounceCooldown = 0.2f;
                break;
            }
            else if (normal.x > 0.4f && Mathf.Abs(normal.y) < 0.7f && ceilingWalkDir < 0f)
            {
                ceilingWalkDir = 1f;
                bounceCooldown = 0.2f;
                break;
            }
        }
    }

    private bool CheckHorizontalObstacle(float dirX)
    {
        if (bodyCollider == null) return false;

        var filter = new ContactFilter2D { useTriggers = false };
        if (ceilingPassLayers.value != 0)
            filter.SetLayerMask(~ceilingPassLayers.value);

        var hits = new RaycastHit2D[8];
        Vector2 checkDir = new Vector2(dirX, 0f);
        int count = bodyCollider.Cast(checkDir, filter, hits, 0.15f);

        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider == null || hit.collider.isTrigger || hit.collider == bodyCollider) continue;
            if (hit.collider.GetComponentInParent<TestAnimal>() == this) continue;

            // ตรวจเฉพาะชนสิ่งกีดขวางแนวตั้งซ้ายขวาเท่านั้น
            if (Mathf.Abs(hit.normal.x) > 0.35f && Mathf.Abs(hit.normal.y) < 0.7f)
            {
                return true;
            }
        }
        return false;
    }

    private IEnumerator ComboAutoRoutine(Vector2 dir)
    {
        if (comboMode == GoatMode.Ceiling)
        {
            float sx = (dir.x < 0f) ? -1f : 1f;
            ceilingWalkDir = sx;

            if (!ExecuteAbility(rb.position + dir)) yield break;
            while (ceilingState == CeilingState.Rising) yield return null;

            // อยู่บนเพดาน เดินไป-กลับซ้ายขวาเรื่อยๆ ตลอดไป ไม่ต้องร่วงลงมา
            yield break;
        }

        if (comboMode == GoatMode.SkywardLaser)
        {
            FaceDirection(dir);
            // เสกเลเซอร์ลงมาจากฟ้า: ค้นหาเป้าหมายอัตโนมัติเฉพาะด้านหน้าที่แพะหันไป (ตามจำนวน skywardTargetCount)
            ExecuteAbility(rb.position + dir);
            if (skywardTargetCount > 1 && skywardMultiStrikeInterval > 0f)
            {
                yield return new WaitForSeconds(skywardTargetCount * skywardMultiStrikeInterval);
            }
            yield break;
        }

        ExecuteAbility(rb.position + dir);
    }

    private bool PerformComboAbility(Vector2 aimWorldPoint, Vector2 dir)
    {
        if (comboMode == GoatMode.ForwardLaser)
        {
            FacingDirection = dir;
            FaceDirection(dir); // เลเซอร์ออกข้างหน้า หันไปตามทิศที่ยิง
            return FireForwardLaser(dir);
        }

        if (comboMode == GoatMode.SkywardLaser)
        {
            Vector2 fwd = GetFacingVector();
            if (!skywardAimFrontOnly)
            {
                FacingDirection = dir;
                FaceDirection(dir);
                fwd = dir;
            }

            int targetCount = Mathf.Max(1, skywardTargetCount);
            var targets = FindSkywardTargets(fwd, targetCount);

            if (targets.Count > 0)
            {
                // ถ้าในโหมดเล่นเอง ผู้เล่นคลิกตรงเป้าหมายที่อยู่ในกรอบ ให้เป้าหมายที่คลิกอยู่คิวแรก
                if (IsInsideSkywardSearchArea(aimWorldPoint, fwd, out _, out _, out _))
                {
                    int matchIdx = targets.FindIndex(p => Vector2.Distance(p, aimWorldPoint) < 0.75f);
                    if (matchIdx > 0)
                    {
                        Vector2 clickedTarget = targets[matchIdx];
                        targets.RemoveAt(matchIdx);
                        targets.Insert(0, clickedTarget);
                    }
                    else if (matchIdx < 0)
                    {
                        targets.Insert(0, aimWorldPoint);
                        if (targets.Count > targetCount) targets.RemoveAt(targets.Count - 1);
                    }
                }

                if (skywardMultiStrikeInterval > 0.001f && targets.Count > 1)
                {
                    StartCoroutine(FireMultipleSkywardLasersRoutine(targets));
                }
                else
                {
                    foreach (var t in targets)
                    {
                        FireSkywardLaser(t);
                    }
                }
                return true;
            }
            else
            {
                Vector2 targetPos = aimWorldPoint;
                if (skywardAimFrontOnly && !IsInsideSkywardSearchArea(aimWorldPoint, fwd, out _, out _, out _))
                {
                    targetPos = rb.position + fwd * Mathf.Min(6f, skywardHorizontalRange);
                }
                return FireSkywardLaser(targetPos);
            }
        }

        FacingDirection = dir;
        switch (comboMode)
        {
            case GoatMode.Havoc: return SmashEverything();
            case GoatMode.Ceiling: return ToggleCeiling();
        }
        return false;
    }

    #region Forward Laser (ยาเขียว: ยิงเลเซอร์ออกข้างหน้า)
    /// <summary>ยาเขียว: ยิงเลเซอร์ออกข้างหน้า สลับประตู/ทำลายหรือดันกล่อง/กดปุ่มสวิตช์ (สลับสถานะ 1 ครั้งต่อนัด) และหยุดเมื่อชน Laser Block Layers</summary>
    private bool FireForwardLaser(Vector2 dir)
    {
        StopMoving();
        Vector2 origin = (laserFirePoint != null) ? (Vector2)laserFirePoint.position : (rb.position + (Vector2)transform.up * 0.15f);
        Vector2 end = origin + dir * laserRange;
        RaycastHit2D targetHit = default;
        bool hasHit = false;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, laserRange);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        var handledDoors = new List<Door>();
        var handledBoxes = new List<PushableBox>();
        var handledSwitches = new List<FloorSwitch>();

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.GetComponentInParent<TestAnimal>() == this) continue;

            // FloorSwitch: เลเซอร์นับเป็นการกดปุ่ม 1 ครั้งต่อนัด (สลับสถานะ ปุ่มยุบแล้วเด้งกลับ เฉพาะถ้าเปิด laserCanToggleSwitches) แล้วทะลุผ่านไปต่อ
            var sw = hit.collider.GetComponentInParent<FloorSwitch>();
            if (sw != null)
            {
                if (laserCanToggleSwitches && !handledSwitches.Contains(sw))
                {
                    handledSwitches.Add(sw);
                    sw.RemoteToggle();
                }
                continue;
            }

            var door = hit.collider.GetComponentInParent<Door>();
            if (door != null)
            {
                if (!handledDoors.Contains(door))
                {
                    handledDoors.Add(door);
                    if (door.IsBreakable)
                    {
                        door.BreakDoor(); // ประตูไม้พังได้ -> ทำลาย
                    }
                    else if (laserCanToggleDoors)
                    {
                        door.RemoteToggle(); // ประตูธรรมดาเปิดได้เฉพาะเมื่อเปิดออปชันไว้
                    }
                }

                // ถ้าประตูยังปิดอยู่ (ไม่ได้เปิดและไม่ได้พัง) ให้เลเซอร์หยุดตรงนี้ ไม่ทะลุผ่าน
                if (!door.IsOpen && !door.IsBroken)
                {
                    end = hit.point;
                    targetHit = hit;
                    hasHit = true;
                    break;
                }

                // ถ้าประตูเปิดอยู่ หรือพังแล้ว เลเซอร์จะทะลุผ่านช่องเปิดไปต่อได้
                continue;
            }

            var box = hit.collider.GetComponentInParent<PushableBox>();
            if (box != null && !handledBoxes.Contains(box))
            {
                handledBoxes.Add(box);
                if (box.CanBeHitByForwardLaser)
                {
                    if (box.IsDestructible && !box.IsBroken)
                        box.BreakBox();                                   // กล่องพังได้ -> ทำลาย
                    else
                        box.TryPush(dir, pushDistance, pushSpeed, null);  // กล่องพังไม่ได้ -> ดัน
                }
            }

            // ชนกำแพง/พื้น/ประตู/กล่อง ใน Layer ที่ตั้งไว้ -> เลเซอร์หยุดตรงนี้
            if (laserBlockLayers.value != 0 && (laserBlockLayers.value & (1 << hit.collider.gameObject.layer)) != 0)
            {
                end = hit.point;
                targetHit = hit;
                hasHit = true;
                break;
            }
        }

        SpawnLaserVisual(origin, end, dir, hasHit, targetHit);
        return true;
    }

    private void SpawnLaserVisual(Vector2 from, Vector2 to, Vector2 dir, bool hasHit, RaycastHit2D hitInfo)
    {
        AudioClip soundToPlay = ResolveLaserSound();
        if (soundToPlay != null)
        {
            PlayAnimalSound(soundToPlay);
        }

        // เอฟเฟกต์ตรงปากกระบอก/จุดยิง (Muzzle Flash)
        if (laserMuzzlePrefab != null)
        {
            Quaternion mRot = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            Instantiate(laserMuzzlePrefab, from, mRot);
        }

        // เอฟเฟกต์ตรงจุดที่ชนโดนสิ่งกีดขวาง (Hit Impact)
        if (hasHit && laserHitPrefab != null)
        {
            Quaternion hRot = (hitInfo.collider != null && hitInfo.normal != Vector2.zero)
                ? Quaternion.FromToRotation(Vector2.up, hitInfo.normal)
                : Quaternion.Euler(0, 0, Mathf.Atan2(-dir.y, -dir.x) * Mathf.Rad2Deg);
            Instantiate(laserHitPrefab, to, hRot);
        }

        // แสดงผลลำแสงเลเซอร์
        if (laserPrefab != null)
        {
            GameObject go = Instantiate(laserPrefab, from, Quaternion.identity);

            // 1. ถ้ามี GoatLaserBeam component
            if (go.TryGetComponent<GoatLaserBeam>(out var beam))
            {
                beam.Setup(from, to, dir, hitInfo);
                return;
            }

            // 2. ถ้ามี LineRenderer ในตัว
            var lr = go.GetComponentInChildren<LineRenderer>();
            if (lr != null)
            {
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);
                Destroy(go, laserDisplaySeconds);
                return;
            }

            // 3. ถ้ามี SpriteRenderer ในตัว (ขยายความยาวสไปรต์)
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                float dist = Vector2.Distance(from, to);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                go.transform.position = (from + to) * 0.5f;
                go.transform.rotation = Quaternion.Euler(0, 0, angle);

                if (sr.drawMode == SpriteDrawMode.Tiled || sr.drawMode == SpriteDrawMode.Sliced)
                {
                    sr.size = new Vector2(dist, sr.size.y);
                }
                else
                {
                    float spriteWidth = (sr.sprite != null && sr.sprite.rect.width > 0f)
                        ? sr.sprite.rect.width / sr.sprite.pixelsPerUnit
                        : 1f;
                    Vector3 scale = go.transform.localScale;
                    scale.x = dist / Mathf.Max(0.01f, spriteWidth);
                    go.transform.localScale = scale;
                }

                Destroy(go, laserDisplaySeconds);
                return;
            }

            Destroy(go, laserDisplaySeconds);
        }
        else
        {
            // ถ้ายังไม่ได้ใส่ Prefab ให้วาดด้วย LineRenderer อัตโนมัติ
            DrawProceduralLaser(from, to);
        }
    }

    private void DrawProceduralLaser(Vector2 from, Vector2 to)
    {
        GameObject go = new GameObject("GoatLaser");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = lr.endWidth = laserWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = laserColor;
        lr.sortingOrder = 50;
        Destroy(go, laserDisplaySeconds);
    }
    #endregion

    #region Skyward Laser (ยา Skyward: เสกเลเซอร์ลงมาจากฟ้า)
    /// <summary>Skyward: เสกลำแสงเลเซอร์ลงมาจากท้องฟ้าใส่เป้าหมาย (ปุ่ม, กล่อง, ประตูไม้)</summary>
    private bool FireSkywardLaser(Vector2 targetPoint)
    {
        StopMoving();

        // จุดเริ่มบนฟ้าเหนือเป้าหมาย
        Vector2 skyOrigin = new Vector2(targetPoint.x, targetPoint.y + skyLaserHeight);
        Vector2 end = targetPoint;
        RaycastHit2D targetHit = default;
        bool hasHit = false;

        float totalCastDistance = skyLaserHeight + 5f;
        RaycastHit2D[] hits = Physics2D.RaycastAll(skyOrigin, Vector2.down, totalCastDistance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        int pierceCount = 0;
        LayerMask effectiveBlockLayers = skywardBlockLayers; // แยกอิสระ 100% ไม่ยืม laserBlockLayers เด็ดขาด

        var handledDoors = new HashSet<Door>();
        var handledBoxes = new HashSet<PushableBox>();
        var handledSwitches = new HashSet<FloorSwitch>();

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.GetComponentInParent<TestAnimal>() == this) continue;

            // 1. ปุ่ม/สวิตช์ (FloorSwitch): โดนเลเซอร์จากฟ้ากดสลับสถานะ (ถ้าเปิด skywardCanToggleSwitches)
            var sw = hit.collider.GetComponentInParent<FloorSwitch>();
            if (sw != null)
            {
                if (skywardCanToggleSwitches && handledSwitches.Add(sw))
                {
                    sw.RemoteToggle();
                }

                if (!skywardPierceSwitches)
                {
                    end = hit.point;
                    targetHit = hit;
                    hasHit = true;
                    break;
                }

                pierceCount++;
                if (pierceCount >= skywardMaxPierceCount)
                {
                    end = hit.point;
                    targetHit = hit;
                    hasHit = true;
                    break;
                }
                continue;
            }

            // 2. ประตู (Door)
            var door = hit.collider.GetComponentInParent<Door>();
            if (door != null)
            {
                if (handledDoors.Add(door))
                {
                    if (door.IsBreakable)
                    {
                        door.BreakDoor(); // ประตูไม้พังได้ -> ทำลาย
                    }
                    else if (skywardCanToggleDoors)
                    {
                        door.RemoteToggle(); // ประตูธรรมดาเปิดได้เฉพาะเมื่อเปิด skywardCanToggleDoors
                    }
                }

                // ถ้าเป็นประตูที่ยังปิดอยู่ (ไม่ได้เปิดและไม่พัง)
                if (!door.IsOpen && !door.IsBroken)
                {
                    if (skywardBlockedByClosedDoors)
                    {
                        end = hit.point;
                        targetHit = hit;
                        hasHit = true;
                        break;
                    }
                }
                else
                {
                    // ประตูเปิดอยู่หรือพังแล้ว
                    if (!skywardPierceOpenDoors)
                    {
                        end = hit.point;
                        targetHit = hit;
                        hasHit = true;
                        break;
                    }
                }

                pierceCount++;
                if (pierceCount >= skywardMaxPierceCount)
                {
                    end = hit.point;
                    targetHit = hit;
                    hasHit = true;
                    break;
                }
                continue;
            }

            // 3. กล่อง (PushableBox)
            var box = hit.collider.GetComponentInParent<PushableBox>();
            if (box != null)
            {
                // ถ้ากล่องนี้ตั้งไว้ว่าไม่ให้โดนเลเซอร์ฟ้าของแพะ (เช่น Chest จบเกม)
                if (!box.CanBeHitBySkywardLaser)
                {
                    if (!skywardPierceSolidBoxes)
                    {
                        end = hit.point;
                        targetHit = hit;
                        hasHit = true;
                        break;
                    }
                    continue;
                }

                if (handledBoxes.Add(box))
                {
                    if (box.IsDestructible && !box.IsBroken)
                    {
                        box.BreakBox(); // กล่องพังได้ -> ทำลาย
                    }
                    else
                    {
                        box.TryPush(Vector2.down, 0.5f, pushSpeed, null);
                    }
                }

                // ตรวจสอบการทะลุผ่านกล่อง
                if (box.IsDestructible)
                {
                    if (!skywardPierceBreakableBoxes)
                    {
                        end = hit.point;
                        targetHit = hit;
                        hasHit = true;
                        break;
                    }
                }
                else
                {
                    if (!skywardPierceSolidBoxes)
                    {
                        end = hit.point;
                        targetHit = hit;
                        hasHit = true;
                        break;
                    }
                }

                pierceCount++;
                if (pierceCount >= skywardMaxPierceCount)
                {
                    end = hit.point;
                    targetHit = hit;
                    hasHit = true;
                    break;
                }
                continue;
            }

            // 4. สิ่งกีดขวาง / กำแพง / เพดาน / พื้น
            bool isAboveTarget = (hit.point.y > targetPoint.y + 0.1f);

            if (isAboveTarget)
            {
                // ถ้าตั้งให้ทะลุกำแพง/เพดาน ให้เลเซอร์ทะลุผ่านลงไปหาเป้าหมายด้านล่างได้เลย
                if (skywardPierceWalls)
                {
                    continue;
                }

                // ถ้าไม่ได้ตั้งให้ทะลุกำแพง และชน Layer ที่บล็อกไว้ ให้หยุดทันที
                if (effectiveBlockLayers.value != 0 && (effectiveBlockLayers.value & (1 << hit.collider.gameObject.layer)) != 0)
                {
                    end = hit.point;
                    targetHit = hit;
                    hasHit = true;
                    break;
                }
            }
            else
            {
                // อยู่ที่ระดับเป้าหมายหรือต่ำกว่า (เช่น พื้นใต้ปุ่ม/กล่อง หรือเล็งใส่พื้น/กำแพงตรงๆ)
                if (effectiveBlockLayers.value != 0 && (effectiveBlockLayers.value & (1 << hit.collider.gameObject.layer)) != 0)
                {
                    end = hit.point;
                    targetHit = hit;
                    hasHit = true;
                    break;
                }

                // ถ้าทะลุผ่านวัตถุมาถึงพื้นแล้ว ให้เลเซอร์หยุดที่จุดตกเป้าหมาย/พื้น
                if (hit.point.y <= targetPoint.y)
                {
                    end = hit.point;
                    targetHit = hit;
                    hasHit = true;
                    break;
                }
            }
        }

        GameObject prefabToUse = (skywardLaserPrefab != null) ? skywardLaserPrefab : laserPrefab;
        GameObject hitEffectToUse = (skywardHitPrefab != null) ? skywardHitPrefab : laserHitPrefab;
        AudioClip soundToUse = (skywardSound != null) ? skywardSound : laserSound;

        SpawnSkywardVisual(skyOrigin, end, prefabToUse, hitEffectToUse, soundToUse, hasHit, targetHit);
        return true;
    }

    private AudioClip ResolveLaserSound()
    {
        if (laserSound != null) return laserSound;
        if (AudioManager.Instance != null)
        {
            AudioClip clip = AudioManager.Instance.GetClip("GoatLaser");
            if (clip != null) { laserSound = clip; return laserSound; }
        }
#if UNITY_EDITOR
        laserSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audioes/Animal/Goat/Goat Green.mp3");
        if (laserSound != null) return laserSound;

        string[] guids = UnityEditor.AssetDatabase.FindAssets("Goat Green t:AudioClip");
        if (guids != null && guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            laserSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            return laserSound;
        }
#endif
        return null;
    }

    private AudioClip ResolveSkywardSound()
    {
        if (skywardSound != null) return skywardSound;
        if (AudioManager.Instance != null)
        {
            AudioClip clip = AudioManager.Instance.GetClip("GoatSkywardLaser")
                          ?? AudioManager.Instance.GetClip("GoatLaser");
            if (clip != null) { skywardSound = clip; return skywardSound; }
        }
#if UNITY_EDITOR
        skywardSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audioes/Animal/Goat/Laser Skyward.mp3");
        if (skywardSound != null) return skywardSound;

        string[] guids = UnityEditor.AssetDatabase.FindAssets("Laser Skyward t:AudioClip");
        if (guids != null && guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            skywardSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            return skywardSound;
        }
#endif
        return null;
    }

    private void SpawnSkywardVisual(Vector2 from, Vector2 to, GameObject prefab, GameObject hitPrefab, AudioClip sound, bool hasHit, RaycastHit2D hitInfo)
    {
        AudioClip soundToPlay = (sound != null) ? sound : ResolveSkywardSound();
        if (soundToPlay != null)
        {
            PlayAnimalSound(soundToPlay);
        }

        if (hasHit && hitPrefab != null)
        {
            Quaternion hRot = (hitInfo.collider != null && hitInfo.normal != Vector2.zero)
                ? Quaternion.FromToRotation(Vector2.up, hitInfo.normal)
                : Quaternion.identity;
            Instantiate(hitPrefab, to, hRot);
        }

        if (prefab != null)
        {
            GameObject go = Instantiate(prefab, from, Quaternion.identity);

            if (go.TryGetComponent<GoatLaserBeam>(out var beam))
            {
                beam.Setup(from, to, Vector2.down, hitInfo);
                return;
            }

            var lr = go.GetComponentInChildren<LineRenderer>();
            if (lr != null)
            {
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);
                Destroy(go, skywardDisplaySeconds);
                return;
            }

            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                float dist = Vector2.Distance(from, to);
                go.transform.position = (from + to) * 0.5f;
                go.transform.rotation = Quaternion.Euler(0, 0, -90f);

                if (sr.drawMode == SpriteDrawMode.Tiled || sr.drawMode == SpriteDrawMode.Sliced)
                {
                    sr.size = new Vector2(dist, sr.size.y);
                }
                else
                {
                    float spriteWidth = (sr.sprite != null && sr.sprite.rect.width > 0f)
                        ? sr.sprite.rect.width / sr.sprite.pixelsPerUnit
                        : 1f;
                    Vector3 s = go.transform.localScale;
                    s.x = dist / Mathf.Max(0.01f, spriteWidth);
                    go.transform.localScale = s;
                }

                Destroy(go, skywardDisplaySeconds);
                return;
            }

            Destroy(go, skywardDisplaySeconds);
        }
        else
        {
            DrawProceduralSkywardLaser(from, to);
        }
    }

    private void DrawProceduralSkywardLaser(Vector2 from, Vector2 to)
    {
        GameObject go = new GameObject("SkywardLaserProcedural");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = lr.endWidth = skywardLaserWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = skywardLaserColor;
        lr.sortingOrder = 50;
        Destroy(go, skywardDisplaySeconds);
    }

    /// <summary>
    /// ตรวจสอบว่าจุดเป้าหมาย targetPos อยู่ในกรอบค้นหาของเลเซอร์ฟ้า (Skyward) หรือไม่
    /// </summary>
    public bool IsInsideSkywardSearchArea(Vector2 targetPos, Vector2 fwd, out float forwardDist, out float vertDist, out float totalDist)
    {
        Vector2 origin = (rb != null) ? rb.position : (Vector2)transform.position;
        Vector2 toTarget = targetPos - origin;

        forwardDist = Vector2.Dot(toTarget, fwd);
        vertDist = Mathf.Abs(toTarget.y - skywardVerticalOffset);
        totalDist = toTarget.magnitude;

        // ตรวจสอบระยะแนวนอน
        if (skywardAimFrontOnly)
        {
            if (forwardDist < 0.1f || forwardDist > skywardHorizontalRange) return false;
        }
        else
        {
            if (Mathf.Abs(toTarget.x) > skywardHorizontalRange) return false;
        }

        // ตรวจสอบระยะแนวตั้ง (สูง-ต่ำ ไม่เกิน skywardVerticalRange)
        if (vertDist > skywardVerticalRange) return false;

        return true;
    }

    private struct SkywardCandidate
    {
        public Vector2 position;
        public float distance;

        public SkywardCandidate(Vector2 pos, float dist)
        {
            position = pos;
            distance = dist;
        }
    }

    /// <summary>
    /// ค้นหาเป้าหมายทั้งหมดในกรอบค้นหาเลเซอร์ฟ้า โดยเรียงจากใกล้ที่สุดไปไกลที่สุด และเลือกมาจำนวน maxCount ชิ้น
    /// </summary>
    public List<Vector2> FindSkywardTargets(Vector2 aimDir, int maxCount)
    {
        Vector2 fwd = (aimDir.sqrMagnitude > 0.001f) ? aimDir.normalized : GetFacingVector();
        var candidates = new List<SkywardCandidate>();

        // 1. ค้นหากล่องในระยะ
        foreach (var box in FindObjectsByType<PushableBox>())
        {
            if (box == null || box.IsBroken) continue;
            if (!box.CanBeHitBySkywardLaser) continue; // ข้ามกล่องที่ปิดไม่ให้โดนเลเซอร์ฟ้า (เช่น หีบสมบัติ Chest)

            if (!IsInsideSkywardSearchArea((Vector2)box.transform.position, fwd, out _, out _, out float dist))
                continue;

            candidates.Add(new SkywardCandidate(box.transform.position, dist));
        }

        // 2. ค้นหาสวิตช์ (เฉพาะเมื่อเปิดให้เลเซอร์ฟ้ากดสวิตช์ได้)
        if (skywardCanToggleSwitches)
        {
            foreach (var sw in FindObjectsByType<FloorSwitch>())
            {
                if (sw == null || sw.IsActive) continue;

                if (!IsInsideSkywardSearchArea((Vector2)sw.transform.position, fwd, out _, out _, out float dist))
                    continue;

                candidates.Add(new SkywardCandidate(sw.transform.position, dist));
            }
        }

        // 3. ค้นหาประตูไม้ที่ยังไม่พัง
        foreach (var door in FindObjectsByType<Door>())
        {
            if (door == null || !door.IsBreakable || door.IsBroken) continue;

            if (!IsInsideSkywardSearchArea((Vector2)door.transform.position, fwd, out _, out _, out float dist))
                continue;

            candidates.Add(new SkywardCandidate(door.transform.position, dist));
        }

        // เรียงลำดับจากใกล้ที่สุดไปไกลที่สุด
        candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

        int count = Mathf.Min(Mathf.Max(1, maxCount), candidates.Count);
        var result = new List<Vector2>(count);
        for (int i = 0; i < count; i++)
        {
            result.Add(candidates[i].position);
        }
        return result;
    }

    private Vector2 FindSkywardTarget(Vector2 aimDir)
    {
        Vector2 fwd = (aimDir.sqrMagnitude > 0.001f) ? aimDir.normalized : GetFacingVector();
        var targets = FindSkywardTargets(fwd, 1);
        if (targets.Count > 0) return targets[0];

        // 4. ถ้าไม่พบเป้าหมาย ให้เสกลงมาข้างหน้าตามทิศที่หัน
        return rb.position + fwd * Mathf.Min(6f, skywardHorizontalRange);
    }

    private IEnumerator FireMultipleSkywardLasersRoutine(List<Vector2> targets)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (i > 0 && skywardMultiStrikeInterval > 0f)
            {
                yield return new WaitForSeconds(skywardMultiStrikeInterval);
            }
            FireSkywardLaser(targets[i]);
        }
    }

    private void EnsureSkywardLaserPrefab()
    {
        if (skywardLaserPrefab == null)
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Laser Skyward t:Prefab");
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                skywardLaserPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
#endif
        }
    }
    #endregion

    /// <summary>Havoc: ทำลายกล่องและประตูทั้งหมดที่พังได้ในด่าน</summary>
    private bool SmashEverything()
    {
        StopMoving();

        AudioClip clip = ResolveHavocSound();
        float vol = (havocVolume > 0.001f) ? havocVolume : 1f;
        if (clip != null)
        {
            PlayAnimalSound(clip, vol);
        }

        Debug.Log($"[{animalName}] Havoc: SmashEverything! Sound: {(clip != null ? clip.name : "None")}");

        foreach (var box in FindObjectsByType<PushableBox>(FindObjectsSortMode.None))
        {
            if (box.IsDestructible && !box.IsBroken) box.BreakBox();
        }
        foreach (var door in FindObjectsByType<Door>(FindObjectsSortMode.None))
        {
            if (door.IsBreakable && !door.IsBroken) door.BreakDoor();
        }
        return true;
    }

    private AudioClip ResolveHavocSound()
    {
        if (havocSound != null) return havocSound;
        if (AudioManager.Instance != null)
        {
            AudioClip clip = AudioManager.Instance.GetClip("GoatHavoc")
                          ?? AudioManager.Instance.GetClip("Havoc")
                          ?? AudioManager.Instance.GetClip("Goat Havoc")
                          ?? AudioManager.Instance.GetClip("GoatSmash");
            if (clip != null) { havocSound = clip; return havocSound; }
        }
#if UNITY_EDITOR
        havocSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audioes/Animal/Goat/Goat Havoc.mp3");
        if (havocSound != null) return havocSound;

        string[] guids = UnityEditor.AssetDatabase.FindAssets("Goat Havoc t:AudioClip");
        if (guids == null || guids.Length == 0) guids = UnityEditor.AssetDatabase.FindAssets("Havoc t:AudioClip");
        if (guids != null && guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            havocSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            return havocSound;
        }
#endif
        return null;
    }

    #region Ceiling Walk (Gravity Potion)
    /// <summary>Surge: อยู่บนพื้น -> กระโดดไปแตะเพดาน / อยู่บนเพดาน -> สลับทิศทางเดินซ้าย-ขวา (ติดอยู่บนเพดานตลอดไปไม่ร่วง)</summary>
    private bool ToggleCeiling()
    {
        switch (ceilingState)
        {
            case CeilingState.Floor:
                if (!TryFindSurface(Vector2.up, out float y))
                {
                    Debug.Log($"[{animalName}] ไม่เจอเพดานเหนือหัว");
                    return false;
                }
                ceilingWalkDir = (FacingDirection.x < 0f) ? -1f : 1f;
                StartCoroutine(RiseToCeilingRoutine(y));
                return true;

            case CeilingState.OnCeiling:
                // ติดอยู่บนเพดานตลอดไป: ถ้ากดใช้ความสามารถอีกครั้งจะสลับทิศทางเดิน
                ceilingWalkDir = -ceilingWalkDir;
                bounceCooldown = 0.2f;
                return true;
        }
        return false; // กำลังขึ้นอยู่
    }

    /// <summary>หาพื้นผิว (เพดาน/พื้น) ในทิศนั้น คืนค่าแกน Y ที่ "กลางตัว" จะไปอยู่เมื่อแตะพื้นผิวนั้น</summary>
    private bool TryFindSurface(Vector2 direction, out float centerY)
    {
        centerY = rb.position.y;

        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(ceilingLayers.value != 0 ? ceilingLayers : (LayerMask)Physics2D.DefaultRaycastLayers);

        var results = new RaycastHit2D[16];
        int count = bodyCollider.Cast(direction, filter, results, ceilingSearchDistance);

        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider2D c = results[i].collider;
            if (c == null || c.isTrigger) continue;
            if (c.GetComponentInParent<PushableBox>() != null) continue; // กล่องไม่ใช่เพดาน/พื้น
            if (c.GetComponentInParent<TestAnimal>() != null) continue;
            best = Mathf.Min(best, results[i].distance);
        }

        if (best == float.MaxValue) return false;

        centerY = rb.position.y + Mathf.Sign(direction.y) * Mathf.Max(0f, best - 0.02f);
        return true;
    }

    private IEnumerator RiseToCeilingRoutine(float targetY)
    {
        ceilingState = CeilingState.Rising;
        groundYBeforeRise = rb.position.y;
        SetExternalMotion(true);
        bodyCollider.excludeLayers = ~0; // ทะลุทุกอย่างระหว่างกระโดดขึ้น

        while (Mathf.Abs(rb.position.y - targetY) > 0.02f)
        {
            float y = Mathf.MoveTowards(rb.position.y, targetY, ceilingRiseSpeed * Time.deltaTime);
            rb.position = new Vector2(rb.position.x, y);
            yield return null;
        }

        ceilingY = targetY;
        rb.position = new Vector2(rb.position.x, ceilingY);
        SetExternalMotion(false);

        bodyCollider.excludeLayers = ceilingPassLayers;
        if (spriteRenderer != null) spriteRenderer.flipY = true; // กลับหัว
        ceilingWalkDir = (FacingDirection.x < 0f) ? -1f : 1f;
        ceilingState = CeilingState.OnCeiling;
    }

    private IEnumerator DropToFloorRoutine()
    {
        if (ceilingState != CeilingState.OnCeiling) yield break;

        // หาพื้นก่อนปล่อยตัว (ถ้าไม่เจอ ตกกลับไปที่เดิมที่เคยยืน)
        if (!TryFindSurface(Vector2.down, out float floorY)) floorY = groundYBeforeRise;

        ceilingState = CeilingState.Falling;
        StopMoving();
        SetExternalMotion(true);
        bodyCollider.excludeLayers = ~0;

        while (Mathf.Abs(rb.position.y - floorY) > 0.02f)
        {
            float y = Mathf.MoveTowards(rb.position.y, floorY, ceilingRiseSpeed * Time.deltaTime);
            rb.position = new Vector2(rb.position.x, y);
            yield return null;
        }

        rb.position = new Vector2(rb.position.x, floorY);
        SetExternalMotion(false);
        bodyCollider.excludeLayers = (LayerMask)0;
        if (spriteRenderer != null) spriteRenderer.flipY = false;
        ceilingState = CeilingState.Floor;
    }
    #endregion
    #endregion

    #region Debug Gizmos
    private void OnDrawGizmos()
    {
        if (showSkywardGizmoAlways)
        {
            DrawSkywardGizmos();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, pushReach);

        if (showSkywardGizmo && !showSkywardGizmoAlways)
        {
            DrawSkywardGizmos();
        }
    }

    private void DrawSkywardGizmos()
    {
        Vector2 pos = (rb != null) ? rb.position : (Vector2)transform.position;
        Vector2 fwd = GetFacingVector();

        float centerX;
        float sizeX;

        if (skywardAimFrontOnly)
        {
            centerX = pos.x + fwd.x * (skywardHorizontalRange * 0.5f);
            sizeX = skywardHorizontalRange;
        }
        else
        {
            centerX = pos.x;
            sizeX = skywardHorizontalRange * 2f;
        }

        float centerY = pos.y + skywardVerticalOffset;
        float sizeY = skywardVerticalRange * 2f;

        Vector3 center = new Vector3(centerX, centerY, 0f);
        Vector3 size = new Vector3(sizeX, sizeY, 0f);

        // 1. วาดพื้นที่สี่เหลี่ยมโปร่งแสง
        Color fillColor = skywardGizmoColor;
        fillColor.a = Mathf.Clamp(skywardGizmoColor.a * 0.35f, 0.05f, 0.3f);
        Gizmos.color = fillColor;
        Gizmos.DrawCube(center, size);

        // 2. วาดเส้นขอบสีเด่นชัด
        Color outlineColor = skywardGizmoColor;
        outlineColor.a = 1f;
        Gizmos.color = outlineColor;
        Gizmos.DrawWireCube(center, size);

        // 3. วาดเส้นลูกศรชี้ทิศไปข้างหน้า
        Gizmos.color = Color.cyan;
        Vector3 arrowStart = new Vector3(pos.x, pos.y, 0f);
        Vector3 arrowEnd = new Vector3(pos.x + fwd.x * skywardHorizontalRange, pos.y, 0f);
        Gizmos.DrawLine(arrowStart, arrowEnd);

        Vector3 arrowHead1 = arrowEnd - (Vector3)(fwd * 0.5f) + Vector3.up * 0.3f;
        Vector3 arrowHead2 = arrowEnd - (Vector3)(fwd * 0.5f) + Vector3.down * 0.3f;
        Gizmos.DrawLine(arrowEnd, arrowHead1);
        Gizmos.DrawLine(arrowEnd, arrowHead2);

        // 4. ไฮไลต์เป้าหมายที่จะถูกผ่าเลเซอร์ N ชิ้นที่อยู่ใกล้สุด
        var targets = FindSkywardTargets(fwd, Mathf.Max(1, skywardTargetCount));
        for (int i = 0; i < targets.Count; i++)
        {
            Vector3 tPos = targets[i];
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(tPos, 0.45f);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.75f);
            Gizmos.DrawLine(new Vector3(tPos.x, tPos.y + 3.5f, 0f), tPos);
        }
    }
    #endregion
}
