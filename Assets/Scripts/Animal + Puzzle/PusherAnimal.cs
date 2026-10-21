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
    private enum GoatMode { Push, Laser, Havoc, Ceiling }
    private enum CeilingState { Floor, Rising, OnCeiling, Falling }

    [Header("Skyward (เขียว+ฟ้า): เสกลำแสงเลเซอร์")]
    [Tooltip("ระยะยิงเลเซอร์ (สลับประตู ทำลายกล่องที่พังได้ ดันกล่องที่พังไม่ได้ ไม่กดปุ่ม/สวิตช์)")]
    [SerializeField] private float laserRange = 12f;

    [Tooltip("Layer ที่ทำให้เลเซอร์หยุดทันที (เช่น กำแพง พื้น ประตู กล่อง) เว้นว่าง = ทะลุทุกอย่าง")]
    [SerializeField] private LayerMask laserBlockLayers;

    [SerializeField] private Color laserColor = new Color(0.4f, 0.9f, 1f);

    [Tooltip("เลเซอร์ค้างให้เห็นกี่วินาที")]
    [SerializeField] private float laserDisplaySeconds = 0.3f;

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
        return potion == pushPotion || (IsDualMode && potion == smashPotion) || IsComboPotion(potion);
    }

    protected override void OnPotionDrunk(PotionType potion)
    {
        switch (potion)
        {
            case PotionType.Skyward: comboMode = GoatMode.Laser; break;
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

        if (comboMode != GoatMode.Push) return PerformComboAbility(dir.normalized);

        if (snapToCardinalDirections)
        {
            dir = (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                ? new Vector2(Mathf.Sign(dir.x), 0f)
                : new Vector2(0f, Mathf.Sign(dir.y));
        }
        dir.Normalize();
        FacingDirection = dir;

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
    }

    protected override void Update()
    {
        base.Update();

        // เมื่ออยู่บนเพดาน: ตัวกลับหัว และหันซ้ายขวาตามทิศที่กำลังเดิน
        if (ceilingState == CeilingState.OnCeiling && spriteRenderer != null)
        {
            spriteRenderer.flipY = true;
            bool goingLeft = ceilingWalkDir < 0f;
            spriteRenderer.flipX = spriteFacesRight ? goingLeft : !goingLeft;
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

        ExecuteAbility(rb.position + dir);
    }

    private bool PerformComboAbility(Vector2 dir)
    {
        FacingDirection = dir;

        switch (comboMode)
        {
            case GoatMode.Laser: return FireLaser(dir);
            case GoatMode.Havoc: return SmashEverything();
            case GoatMode.Ceiling: return ToggleCeiling();
        }
        return false;
    }

    #region Laser
    /// <summary>Skyward: ยิงเลเซอร์ตามแนวตรง สลับประตู/ทำลายหรือดันกล่อง (ไม่กดปุ่ม/สวิตช์) และหยุดเมื่อชน Laser Block Layers</summary>
    private bool FireLaser(Vector2 dir)
    {
        StopMoving();
        Vector2 origin = rb.position;
        Vector2 end = origin + dir * laserRange;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, laserRange);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        var handledDoors = new List<Door>();
        var handledBoxes = new List<PushableBox>();

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.GetComponentInParent<TestAnimal>() == this) continue;

            // ข้าม FloorSwitch เลเซอร์จะไม่กดปุ่ม/สวิตช์
            if (hit.collider.GetComponentInParent<FloorSwitch>() != null) continue;

            var door = hit.collider.GetComponentInParent<Door>();
            if (door != null && !handledDoors.Contains(door))
            {
                handledDoors.Add(door);
                if (door.IsBreakable) door.BreakDoor();
                else door.RemoteToggle();
            }

            var box = hit.collider.GetComponentInParent<PushableBox>();
            if (box != null && !handledBoxes.Contains(box))
            {
                handledBoxes.Add(box);
                if (box.IsDestructible && !box.IsBroken)
                    box.BreakBox();                                   // กล่องพังได้ -> ทำลาย (เหมือน Havoc ไม่เช็กสิทธิ์ของแมวชนิดนี้)
                else
                    box.TryPush(dir, pushDistance, pushSpeed, null);  // กล่องพังไม่ได้ -> ดัน
            }

            // ชนกำแพง/พื้น/ประตู/กล่อง ใน Layer ที่ตั้งไว้ -> เลเซอร์หยุดตรงนี้
            if ((laserBlockLayers.value & (1 << hit.collider.gameObject.layer)) != 0)
            {
                end = hit.point;
                break;
            }
        }

        DrawLaser(origin, end);
        return true;
    }

    private void DrawLaser(Vector2 from, Vector2 to)
    {
        GameObject go = new GameObject("GoatLaser");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = lr.endWidth = 0.12f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = laserColor;
        lr.sortingOrder = 50;
        Destroy(go, laserDisplaySeconds);
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
