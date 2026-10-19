using System.Collections;
using UnityEngine;

/// <summary>
/// กล่องที่ถูกดันได้ (โดย PusherAnimal เท่านั้น สัตว์ตัวอื่นเดินชนไม่ขยับ)
/// เลื่อนเป็นเส้นตรง หยุดเองเมื่อชน Layer ที่ตั้งไว้ใน Obstacle Layer
/// มีแรงโน้มถ่วงจำลอง (ยังเป็น Kinematic แมวเดินชนแล้วกล่องไม่เลื่อนเอง)
/// ต้องมี Rigidbody2D (Kinematic) + Collider2D (แบบไม่ Trigger เพื่อให้สัตว์ชนติด)
/// กล่องทับ FloorSwitch ได้ (สวิตช์ตั้งให้รับกล่องไว้)
/// สามารถตั้งค่าให้ "พังได้" (Destructible):
/// - ติ๊กเลือกชนิดของสัตว์ที่ทำลายกล่องได้ (แมวผลักกล่อง, แมวบิน, แมวก้อนขน)
/// - ปรับจำนวนรอบที่ชนแตกได้ ทุกรอบที่ชน ตัวชนจะกระเด็นถอยหลังแล้วพุ่งกลับมาชนซ้ำจนแตก
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PushableBox : MonoBehaviour
{
    #region Settings
    [Header("Collision")]
    [Tooltip("Layer ที่ขวางกล่อง (กำแพง ประตู พื้น กล่องอื่น ฯลฯ) ห้ามรวม Layer ของสวิตช์ เพราะกล่องต้องทับสวิตช์ได้")]
    [SerializeField] private LayerMask obstacleLayer;

    [Tooltip("ช่องว่างเล็กๆ ที่เว้นไว้ระหว่างกล่องกับสิ่งที่ชน กันกล่องติด")]
    [SerializeField] private float skinWidth = 0.02f;

    [Tooltip("ถ้าเลื่อนได้น้อยกว่านี้ถือว่าดันไม่ไป (ติดกำแพงอยู่แล้ว)")]
    [SerializeField] private float minPushDistance = 0.05f;
    #endregion

    #region Destructible
    [Header("Destructible Settings (กล่องพังได้)")]
    [Tooltip("ติ๊กไว้ = กล่องนี้ทำลายได้ เมื่อถูกชนตัวชนจะกระเด็นถอยหลังแล้วพุ่งกลับมาชนจนแตก")]
    [SerializeField] private bool isDestructible = false;

    [Tooltip("จำนวนครั้งที่ต้องชนจนกล่องแตก")]
    [SerializeField, Min(1)] private int hitsToBreak = 3;

    [Tooltip("ติ๊กไว้ = ดันเลื่อนได้ปกติจนกว่าจะติดกำแพงถึงจะเริ่มชนแตก / ไม่ติ๊ก = เริ่มกระบวนการชนแตกทันทีที่ถูกชน")]
    [SerializeField] private bool breakOnlyWhenBlocked = false;

    [Tooltip("ระยะที่ตัวชนกระเด็นถอยหลังในแต่ละรอบ (หน่วย Unity)")]
    [SerializeField] private float recoilDistance = 1.3f;

    [Tooltip("ความเร็วตอนกระเด็นถอยหลัง")]
    [SerializeField] private float recoilSpeed = 6f;

    [Tooltip("ความเร็วตอนพุ่งกลับไปชนกล่อง")]
    [SerializeField] private float chargeSpeed = 9f;

    [Tooltip("เวลาหยุดตั้งหลักหลังกระเด็น ก่อนพุ่งกลับไปชน (วินาที)")]
    [SerializeField] private float pauseBeforeCharge = 0.25f;

    [Header("Who Can Destroy (ชนิดของสัตว์ที่ทำลายกล่องนี้ได้)")]
    [Tooltip("ติ๊กไว้ = แมวพละกำลัง / แมวผลักกล่อง (Strong Cat / PusherAnimal) ทำลายกล่องนี้ได้")]
    [SerializeField] private bool canBeDestroyedByPusher = true;

    [Tooltip("ติ๊กไว้ = แมวบิน (Fly CAT / FlyingAnimal) ทำลายกล่องนี้ได้")]
    [SerializeField] private bool canBeDestroyedByFlyer = false;

    [Tooltip("ติ๊กไว้ = แมวก้อนขน (Fur Ball Cat / HairballAnimal) ทำลายกล่องนี้ได้")]
    [SerializeField] private bool canBeDestroyedByHairball = false;

    [Tooltip("ติ๊กไว้ = สัตว์ชนิดอื่นๆ นอกเหนือจากนี้ทำลายกล่องนี้ได้")]
    [SerializeField] private bool canBeDestroyedByOthers = false;

    [Tooltip("ติ๊กไว้ = สัตว์ต้องได้รับยาก่อน (สถานะพละกำลัง/ได้รับยา) ถึงจะทำลายกล่องได้")]
    [SerializeField] private bool requirePoweredAnimal = false;

    [Header("Destructible Visuals")]
    [Tooltip("สไปรต์เมื่อกล่องแตก (ถ้าเว้นว่างไว้จะซ่อนหรือลบกล่องทิ้ง)")]
    [SerializeField] private Sprite brokenSprite;

    [Tooltip("Prefab เอฟเฟกต์ตอนกล่องแตก (Particle เศษกล่อง)")]
    [SerializeField] private GameObject breakEffectPrefab;

    [Tooltip("Prefab เอฟเฟกต์ตอนกล่องโดนชนแต่ละครั้ง (Hit effect / ฝุ่น)")]
    [SerializeField] private GameObject hitEffectPrefab;

    [Tooltip("สั่นตัวกล่องเล็กน้อยตอนโดนชน")]
    [SerializeField] private bool shakeOnHit = true;

    [Tooltip("ลบ GameObject กล่องทิ้งทันทีเมื่อแตก")]
    [SerializeField] private bool destroyOnBreak = true;
    #endregion

    #region Gravity
    [Header("Gravity (แรงโน้มถ่วง)")]
    [Tooltip("ติ๊กไว้ = กล่องตกตามแรงโน้มถ่วง (ใช้ค่า Physics 2D Gravity ของโปรเจกต์)")]
    [SerializeField] private bool useGravity = true;

    [Tooltip("ตัวคูณแรงโน้มถ่วง (1 = เท่าค่าใน Project Settings)")]
    [SerializeField] private float gravityScale = 1f;

    [Tooltip("ความเร็วตกสูงสุด กันกล่องทะลุพื้น")]
    [SerializeField] private float maxFallSpeed = 20f;
    #endregion

    #region State
    private Rigidbody2D rb;
    private Collider2D col;
    private bool isMoving = false;
    private float verticalSpeed = 0f;
    private int gravityHoldCount = 0;
    private readonly RaycastHit2D[] castResults = new RaycastHit2D[8];

    public bool IsMoving => isMoving;

    [Header("Weight")]
    [Tooltip("มวลของกล่อง ใช้กับสวิตช์ที่ต้องใช้น้ำหนัก (FloorSwitch.Required Weight)")]
    [SerializeField] private float mass = 2f;
    public float Mass => mass;
    public bool IsGravityHeld => gravityHoldCount > 0;
    public bool IsDestructible => isDestructible;
    public bool IsBroken { get; private set; } = false;
    public bool IsProcessingHit => isProcessingHit;
    public int CurrentHits { get; private set; } = 0;

    private bool isProcessingHit = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f; // จำลองเอง เพื่อไม่ให้แมวเดินชนแล้วกล่องเลื่อน
        col = GetComponent<Collider2D>();
    }

    private void FixedUpdate()
    {
        if (isMoving || isProcessingHit || IsBroken) return; // ตอนถูกดันหรือชนอยู่ SlideRoutine / Knockback จัดการแล้ว
        ApplyMove(GravityDelta());
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isDestructible || IsBroken || isProcessingHit) return;

        TestAnimal animal = collision.gameObject.GetComponentInParent<TestAnimal>();
        if (animal != null && CanAnimalDestroyBox(animal))
        {
            Vector2 hitDir = ((Vector2)transform.position - (Vector2)animal.transform.position).normalized;
            TriggerDestructionSequence(animal, hitDir);
        }
    }
    #endregion

    #region Destructible API
    /// <summary>
    /// ตรวจสอบว่าสัตว์ตัวนี้มีสิทธิ์ทำลายกล่องนี้หรือไม่ (ตามชนิดที่ติ๊กเลือกไว้ใน Inspector)
    /// </summary>
    public bool CanAnimalDestroyBox(TestAnimal animal)
    {
        if (animal == null) return false;

        // ถ้าตั้งไว้ว่าต้องได้รับยาก่อน
        if (requirePoweredAnimal && !animal.CanUseAbility) return false;

        // ตรวจสอบตามชนิดของสัตว์
        if (animal is PusherAnimal pusherAnimal)
        {
            return canBeDestroyedByPusher && pusherAnimal.CanSmash;
        }

        if (animal.RequiredPotionType == PotionType.Strength)
        {
            return canBeDestroyedByPusher;
        }

        if (animal is FlyingAnimal ramCow && ramCow.IsRamming) return true;

        if (animal is FlyingAnimal || animal.RequiredPotionType == PotionType.Light)
        {
            return canBeDestroyedByFlyer;
        }

        if (animal is HairballAnimal || animal.RequiredPotionType == PotionType.Vomit)
        {
            return canBeDestroyedByHairball;
        }

        return canBeDestroyedByOthers;
    }

    /// <summary>
    /// เริ่มกระบวนการชนทำลายกล่อง (ตัวชนกระเด็นถอยหลัง -> พุ่งกลับมาชนซ้ำ จนครบจำนวน hitsToBreak)
    /// </summary>
    public void TriggerDestructionSequence(TestAnimal animal, Vector2 hitDirection)
    {
        if (!isDestructible || IsBroken || isProcessingHit || animal == null) return;
        if (!CanAnimalDestroyBox(animal)) return;

        StartCoroutine(DestructionSequenceRoutine(animal, hitDirection));
    }

    private IEnumerator DestructionSequenceRoutine(TestAnimal animal, Vector2 hitDirection)
    {
        isProcessingHit = true;
        animal.SetExternalMotion(true);

        Rigidbody2D animalRb = animal.GetComponent<Rigidbody2D>();

        // หาแนวทิศทางชนและทิศทางกระเด็น
        Vector2 attackDir = hitDirection.normalized;
        if (attackDir.sqrMagnitude < 0.001f)
        {
            attackDir = ((Vector2)transform.position - animalRb.position).normalized;
            if (attackDir.sqrMagnitude < 0.001f)
                attackDir = animal.FacingDirection;
        }
        Vector2 recoilDir = -attackDir;

        while (CurrentHits < hitsToBreak && !IsBroken)
        {
            CurrentHits++;
            Debug.Log($"[PushableBox] {name} โดนชนครั้งที่ {CurrentHits}/{hitsToBreak}!");

            if (shakeOnHit)
            {
                StartCoroutine(ShakeRoutine(0.15f, 0.08f));
            }

            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            }

            // ถ้าชนจนครบจำนวนครั้งแล้ว -> กล่องแตก!
            if (CurrentHits >= hitsToBreak)
            {
                BreakBox();
                break;
            }

            // --- 1. กระเด็นถอยหลัง (Recoil) ---
            Vector2 recoilStart = animalRb.position;
            float actualRecoilDist = recoilDistance;

            // ตรวจสอบสิ่งกีดขวางข้างหลังเพื่อไม่ให้กระเด็นทะลุกำแพง
            RaycastHit2D obstacleCheck = Physics2D.CircleCast(recoilStart, 0.25f, recoilDir, recoilDistance, obstacleLayer);
            if (obstacleCheck.collider != null && obstacleCheck.collider.gameObject != gameObject)
            {
                actualRecoilDist = Mathf.Max(0.2f, obstacleCheck.distance - 0.1f);
            }
            Vector2 recoilTarget = recoilStart + recoilDir * actualRecoilDist;

            float recoilDuration = Mathf.Max(0.05f, actualRecoilDist / Mathf.Max(recoilSpeed, 0.1f));
            float recoilElapsed = 0f;
            while (recoilElapsed < recoilDuration)
            {
                recoilElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(recoilElapsed / recoilDuration);
                // Ease-out
                float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);
                animalRb.position = Vector2.Lerp(recoilStart, recoilTarget, smoothT);
                yield return null;
            }

            // --- 2. หยุดตั้งหลักชั่วขณะ (Pause before charge) ---
            yield return new WaitForSeconds(pauseBeforeCharge);

            // --- 3. พุ่งกลับไปชนอีกรอบ (Charge back) ---
            Vector2 chargeStart = animalRb.position;
            Vector2 boxSurface = (Vector2)transform.position - attackDir * 0.85f;
            float chargeDist = Vector2.Distance(chargeStart, boxSurface);
            float chargeDuration = Mathf.Max(0.05f, chargeDist / Mathf.Max(chargeSpeed, 0.1f));
            float chargeElapsed = 0f;

            while (chargeElapsed < chargeDuration)
            {
                chargeElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(chargeElapsed / chargeDuration);
                // Ease-in (เร่งความเร็วพุ่งกระแทก)
                float smoothT = t * t;
                animalRb.position = Vector2.Lerp(chargeStart, boxSurface, smoothT);
                yield return null;
            }
        }

        if (animal != null)
        {
            animal.SetExternalMotion(false);
        }
        isProcessingHit = false;
    }

    /// <summary>
    /// สั่งทำลายกล่องทันที
    /// </summary>
    public void BreakBox()
    {
        if (IsBroken) return;
        IsBroken = true;

        Debug.Log($"[PushableBox] กล่อง {name} แตกแล้ว!");

        if (breakEffectPrefab != null)
        {
            Instantiate(breakEffectPrefab, transform.position, Quaternion.identity);
        }

        if (col != null)
        {
            col.enabled = false;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (brokenSprite != null && sr != null)
        {
            sr.sprite = brokenSprite;
        }
        else if (destroyOnBreak)
        {
            Destroy(gameObject);
        }
        else if (sr != null)
        {
            sr.enabled = false;
        }
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        Vector3 originalPos = transform.position;
        float elapsed = 0f;
        while (elapsed < duration && !IsBroken)
        {
            elapsed += Time.deltaTime;
            float x = Random.Range(-magnitude, magnitude);
            float y = Random.Range(-magnitude, magnitude);
            transform.position = originalPos + new Vector3(x, y, 0f);
            yield return null;
        }
        if (!IsBroken) transform.position = originalPos;
    }
    #endregion

    #region Gravity API
    /// <summary>สายพานเรียกเพื่อพักแรงโน้มถ่วง กันกล่องตกทะลุสายพานที่เป็น Trigger</summary>
    public void AddGravityHold()
    {
        gravityHoldCount++;
        verticalSpeed = 0f;
    }

    public void RemoveGravityHold()
    {
        gravityHoldCount = Mathf.Max(0, gravityHoldCount - 1);
    }
    #endregion

    #region Push API
    /// <summary>ดันกล่องไปทิศ direction ไกลสุด distance คืนค่า true ถ้าดันไปได้จริง</summary>
    public bool TryPush(Vector2 direction, float distance, float speed, TestAnimal pusher = null)
    {
        if (isMoving || isProcessingHit || IsBroken) return false;

        // ถ้ากล่องนี้ตั้งให้พังได้ และสัตว์ตัวที่ผลักมีสิทธิ์ทำลายกล่องนี้
        if (isDestructible && pusher != null && CanAnimalDestroyBox(pusher))
        {
            if (!breakOnlyWhenBlocked)
            {
                TriggerDestructionSequence(pusher, direction);
                return true;
            }
            else
            {
                direction.Normalize();
                float freeDistance = GetFreeDistance(direction, distance);
                if (freeDistance < minPushDistance)
                {
                    // ชนติดกำแพง ดันต่อไม่ได้ -> เริ่มกระบวนการชนทำลาย
                    TriggerDestructionSequence(pusher, direction);
                    return true;
                }
            }
        }

        direction.Normalize();
        float freeDist = GetFreeDistance(direction, distance);
        if (freeDist < minPushDistance) return false;

        StartCoroutine(SlideRoutine(direction, freeDist, speed));
        return true;
    }

    /// <summary>วัดว่าเลื่อนไปทางนั้นได้ไกลสุดเท่าไร ก่อนจะชนสิ่งกีดขวาง</summary>
    private float GetFreeDistance(Vector2 direction, float maxDistance)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleLayer);
        filter.useTriggers = false;

        int count = col.Cast(direction, filter, castResults, maxDistance);
        float free = maxDistance;
        for (int i = 0; i < count; i++)
        {
            free = Mathf.Min(free, castResults[i].distance - skinWidth);
        }
        return Mathf.Max(free, 0f);
    }

    private IEnumerator SlideRoutine(Vector2 direction, float distance, float speed)
    {
        isMoving = true;
        float moved = 0f;

        while (moved < distance)
        {
            float step = Mathf.Min(speed * Time.fixedDeltaTime, distance - moved);
            ApplyMove(direction * step + GravityDelta());
            moved += step;
            yield return new WaitForFixedUpdate();
        }

        isMoving = false;
    }

    private Vector2 GravityDelta()
    {
        if (!useGravity || gravityHoldCount > 0 || gravityScale <= 0f)
        {
            verticalSpeed = 0f;
            return Vector2.zero;
        }

        verticalSpeed += Physics2D.gravity.y * gravityScale * Time.fixedDeltaTime;
        verticalSpeed = Mathf.Max(verticalSpeed, -maxFallSpeed);

        if (Mathf.Abs(verticalSpeed) < 0.0001f) return Vector2.zero;

        bool falling = verticalSpeed < 0f;
        Vector2 dir = falling ? Vector2.down : Vector2.up;
        float desired = Mathf.Abs(verticalSpeed) * Time.fixedDeltaTime;
        float free = GetFreeDistance(dir, desired);
        if (free < desired - 0.0001f) verticalSpeed = 0f;
        return dir * free;
    }

    private void ApplyMove(Vector2 delta)
    {
        if (delta.sqrMagnitude < 0.0000001f) return;
        rb.MovePosition(rb.position + delta);
    }
    #endregion
}
