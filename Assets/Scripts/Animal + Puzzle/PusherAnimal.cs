using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    [Tooltip("รัศมีค้นหาเป้าหมายอัตโนมัติ (ปุ่ม/กล่อง/ประตู) เมื่อแพะได้ยา Skyward")]
    [SerializeField] private float skywardAutoSearchRadius = 25f;

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
            // ยาเขียว (ยิงเลเซอร์ออกข้างหน้า): ยิงไปตามแกน X ของแพะที่หมุนไว้
            if (comboMode == GoatMode.ForwardLaser)
                dir = ((Vector2)transform.right * Mathf.Sign(transform.lossyScale.x)).normalized;
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

        for (int i = 0; i < autoPushCount; i++)
        {
            if (i > 0) yield return new WaitForSeconds(abilityCooldown);

            // เดินไปยืนด้านหลังกล่อง (ฝั่งตรงข้ามทิศที่จะผลัก)
            Vector2 standPos = (Vector2)box.transform.position - dir * standOffset;
            MoveTo(standPos);
            while (IsMoving) yield return null;

            // ผลัก
            if (!ExecuteAbility(rb.position + dir)) yield break; // ผลักไม่ไปแล้ว (ติดกำแพง) เลิก

            while (box != null && (box.IsMoving || box.IsProcessingHit)) yield return null;
        }
    }

    private PushableBox FindNearestBox()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, autoSearchRadius, boxLayer);
        PushableBox best = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<PushableBox>(out var b)) continue;
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

        Sprite s = smashMode ? smashFormSprite : pushFormSprite;
        if (s != null && spriteRenderer != null) spriteRenderer.sprite = s;

        Debug.Log($"[{animalName}] โหมด: {(smashMode ? "ทำลายกล่อง" : "ดันกล่อง")}");
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
            box.TriggerDestructionSequence(this, dir);
            return true;
        }

        StopMoving();
        return box.TryPush(dir, pushDistance, pushSpeed, this);
    }

    private PushableBox FindBoxInFront(Vector2 dir)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, pushReach, boxLayer);
        PushableBox best = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<PushableBox>(out var box)) continue;

            Vector2 toBox = (Vector2)box.transform.position - rb.position;
            if (Vector2.Dot(toBox.normalized, dir) < aimAlignment) continue;

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
            // เสกเลเซอร์ลงมาจากฟ้า: ค้นหาเป้าหมายอัตโนมัติ (กล่อง/ปุ่มสวิตช์/ประตูไม้)
            Vector2 target = FindSkywardTarget(dir);
            ExecuteAbility(target);
            yield break;
        }

        ExecuteAbility(rb.position + dir);
    }

    private bool PerformComboAbility(Vector2 aimWorldPoint, Vector2 dir)
    {
        FacingDirection = dir;
        if (comboMode == GoatMode.ForwardLaser) FaceDirection(dir); // เลเซอร์ออกข้างหน้า หันไปตามทิศที่ยิง

        switch (comboMode)
        {
            case GoatMode.ForwardLaser: return FireForwardLaser(dir);
            case GoatMode.SkywardLaser: return FireSkywardLaser(aimWorldPoint);
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

            // FloorSwitch: เลเซอร์นับเป็นการกดปุ่ม 1 ครั้งต่อนัด (สลับสถานะ ปุ่มยุบแล้วเด้งกลับ) แล้วทะลุผ่านไปต่อ
            var sw = hit.collider.GetComponentInParent<FloorSwitch>();
            if (sw != null)
            {
                if (!handledSwitches.Contains(sw))
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
                if (box.IsDestructible && !box.IsBroken)
                    box.BreakBox();                                   // กล่องพังได้ -> ทำลาย
                else
                    box.TryPush(dir, pushDistance, pushSpeed, null);  // กล่องพังไม่ได้ -> ดัน
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
        if (laserSound != null)
        {
            AudioSource.PlayClipAtPoint(laserSound, from);
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

            // 1. ปุ่ม/สวิตช์ (FloorSwitch): โดนเลเซอร์จากฟ้ากดสลับสถานะ
            var sw = hit.collider.GetComponentInParent<FloorSwitch>();
            if (sw != null)
            {
                if (handledSwitches.Add(sw))
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

    private void SpawnSkywardVisual(Vector2 from, Vector2 to, GameObject prefab, GameObject hitPrefab, AudioClip sound, bool hasHit, RaycastHit2D hitInfo)
    {
        if (sound != null)
        {
            AudioSource.PlayClipAtPoint(sound, to);
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

    private Vector2 FindSkywardTarget(Vector2 aimDir)
    {
        // 1. ค้นหากล่องในระยะ
        PushableBox bestBox = null;
        float bestBoxDist = float.MaxValue;
        foreach (var box in FindObjectsByType<PushableBox>())
        {
            if (box == null || box.IsBroken) continue;
            float d = Vector2.Distance(rb.position, box.transform.position);
            if (d <= skywardAutoSearchRadius && d < bestBoxDist)
            {
                bestBoxDist = d;
                bestBox = box;
            }
        }
        if (bestBox != null) return bestBox.transform.position;

        // 2. ค้นหาสวิตช์ที่ยังไม่เปิด
        FloorSwitch bestSwitch = null;
        float bestSwitchDist = float.MaxValue;
        foreach (var sw in FindObjectsByType<FloorSwitch>())
        {
            if (sw == null || sw.IsActive) continue;
            float d = Vector2.Distance(rb.position, sw.transform.position);
            if (d <= skywardAutoSearchRadius && d < bestSwitchDist)
            {
                bestSwitchDist = d;
                bestSwitch = sw;
            }
        }
        if (bestSwitch != null) return bestSwitch.transform.position;

        // 3. ค้นหาประตูไม้ที่ยังไม่พัง
        Door bestDoor = null;
        float bestDoorDist = float.MaxValue;
        foreach (var door in FindObjectsByType<Door>())
        {
            if (door == null || !door.IsBreakable || door.IsBroken) continue;
            float d = Vector2.Distance(rb.position, door.transform.position);
            if (d <= skywardAutoSearchRadius && d < bestDoorDist)
            {
                bestDoorDist = d;
                bestDoor = door;
            }
        }
        if (bestDoor != null) return bestDoor.transform.position;

        // 4. ถ้าไม่พบเป้าหมาย ให้เสกลงมาข้างหน้า
        return rb.position + aimDir * 6f;
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
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, pushReach);
    }
    #endregion
}
