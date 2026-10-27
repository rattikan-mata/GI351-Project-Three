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

    [Header("Goat Laser Interaction (การโดนเลเซอร์ของแพะ)")]
    [Tooltip("ติ๊กไว้ = กล่องนี้โดนเลเซอร์จากฟ้า (ยา Skyward ของแพะ) ได้\n" +
             "ไม่ติ๊ก = เลเซอร์จากฟ้าจะไม่เล็งเป้าและไม่ทำลาย/ไม่ดันกล่องนี้ (เช่น หีบสมบัติ/Chest จบเกม)")]
    [SerializeField] private bool canBeHitBySkywardLaser = true;
    public bool CanBeHitBySkywardLaser => canBeHitBySkywardLaser;

    [Tooltip("ติ๊กไว้ = กล่องนี้โดนเลเซอร์ยิงออกข้างหน้า (ยาเขียวของแพะ) ได้\n" +
             "ไม่ติ๊ก = เลเซอร์ข้างหน้าจะไม่ทำลาย/ไม่ดันกล่องนี้")]
    [SerializeField] private bool canBeHitByForwardLaser = true;
    public bool CanBeHitByForwardLaser => canBeHitByForwardLaser;

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

    #region Audio
    [Header("Audio (เสียง)")]
    [Tooltip("เสียงตอนกล่องแตก")]
    [SerializeField] private AudioClip breakSound;
    [Range(0f, 1f)] [SerializeField] private float breakVolume = 1f;

    [Tooltip("เสียงตอนกล่องโดนผลัก/เลื่อน")]
    [SerializeField] private AudioClip pushSound;
    [Range(0f, 1f)] [SerializeField] private float pushVolume = 1f;

    [Tooltip("AudioSource สำหรับเล่นเสียง (ถ้าเว้นว่างจะค้นหาจากตัวกล่องให้อัตโนมัติ)")]
    [SerializeField] private AudioSource audioSource;
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
    public bool IsDestructible
    {
        get => isDestructible;
        set => isDestructible = value;
    }
    public int HitsToBreak
    {
        get => hitsToBreak;
        set => hitsToBreak = Mathf.Max(1, value);
    }
    public bool IsBroken { get; private set; } = false;
    public bool IsProcessingHit => isProcessingHit;
    public int CurrentHits { get; private set; } = 0;

    private bool isProcessingHit = false;

    /// <summary>
    /// สั่งให้กล่องสั่นและปล่อยเอฟเฟกต์โดนชน 1 ครั้ง
    /// </summary>
    public void TriggerHitFeedback()
    {
        if (IsBroken) return;
        if (shakeOnHit)
        {
            StartCoroutine(ShakeRoutine(0.15f, 0.08f));
        }
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f; // จำลองเอง เพื่อไม่ให้แมวเดินชนแล้วกล่องเลื่อน
        col = GetComponent<Collider2D>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
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
        if (animal != null)
        {
            // แพะ (PusherAnimal) และตัวบิน (FlyingAnimal ในโหมด Surge) จะไม่ทุบกล่องแตกเพียงเพราะเดินชน
            if (animal is PusherAnimal) return;
            if (animal is FlyingAnimal flyer && flyer.IsRamming) return;

            if (animal.CanBreakObstacles && CanAnimalDestroyBox(animal))
            {
                Vector2 hitDir = ((Vector2)transform.position - (Vector2)animal.transform.position).normalized;
                TriggerDestructionSequence(animal, hitDir);
            }
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

        PlayBreakSound();

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

    private float lastPushSoundTime = -1f;

    private void PlayPushSound()
    {
        AudioClip clip = pushSound != null ? pushSound : (AudioManager.Instance != null ? AudioManager.Instance.GetClip("BoxPush") : null);
        if (clip == null) return;
        if (Time.time - lastPushSoundTime < 0.2f) return;
        lastPushSoundTime = Time.time;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXAt(clip, transform.position, pushVolume);
        }
        else if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, pushVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, pushVolume);
        }
    }

    private void PlayBreakSound()
    {
        AudioClip clip = breakSound != null ? breakSound : (AudioManager.Instance != null ? AudioManager.Instance.GetClip("BoxBreak") : null);
        if (clip == null) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXAt(clip, transform.position, breakVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, breakVolume);
        }
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

    /// <summary>ดีดกล่องด้วยความเร็ว (เช่น เทรมโพลีน): แกน Y ใช้ระบบแรงโน้มถ่วงของกล่อง ส่วนแกน X จะเลื่อนผ่าน TryPush</summary>
    public void Launch(Vector2 velocity)
    {
        if (IsBroken || isProcessingHit) return;
        verticalSpeed = velocity.y;
        if (Mathf.Abs(velocity.x) > 0.05f)
        {
            TryPush(new Vector2(Mathf.Sign(velocity.x), 0f), Mathf.Abs(velocity.x) * 0.5f, Mathf.Abs(velocity.x));
        }
    }
    #endregion

    /// <summary>Layer ที่เป็นสิ่งกีดขวาง (รวม Wall และ Box อื่นด้วย)</summary>
    public LayerMask GetEffectiveObstacleLayer()
    {
        LayerMask mask = obstacleLayer;
        if (mask.value == 0)
        {
            mask = LayerMask.GetMask("Obstacle", "Wall", "Box");
            if (mask.value == 0) mask = (LayerMask)128; // Layer 7
        }
        else
        {
            int wallMask = LayerMask.GetMask("Wall");
            int boxMask = LayerMask.GetMask("Box");
            mask |= wallMask | boxMask;
        }
        return mask;
    }

    private Coroutine currentSlideCoroutine;
    private Vector2 currentSlideDirection;
    private float remainingSlideDistance = 0f;
    private float currentSlideSpeed = 0f;

    #region Push API
    /// <summary>ดันกล่องไปทิศ direction ไกลสุด distance คืนค่า true ถ้าดันไปได้จริง</summary>
    public bool TryPush(Vector2 direction, float distance, float speed, TestAnimal pusher = null)
    {
        if (isProcessingHit || IsBroken) return false;

        direction.Normalize();

        // 1. ถ้ากล่องกำลังเลื่อนอยู่แล้ว (เช่น โดนฟองสบู่ลูกแรกผลัก แล้วลูกถัดไปมาโดนซ้ำ)
        if (isMoving)
        {
            // ถ้าผลักไปในทิศทางเดียวกัน ให้สะสมระยะทางเพิ่มอย่างต่อเนื่อง
            if (currentSlideCoroutine != null && Vector2.Dot(currentSlideDirection, direction) > 0.8f)
            {
                float addDist = GetFreeDistance(direction, remainingSlideDistance + distance, pusher);
                remainingSlideDistance = addDist;
                currentSlideSpeed = Mathf.Max(currentSlideSpeed, speed);
                PlayPushSound();
                return true;
            }
            return false;
        }

        float freeDist = GetFreeDistance(direction, distance, pusher);

        // 2. ถ้ามีระยะว่างให้เลื่อนกล่องไปข้างหน้าได้ -> เลื่อนกล่องตามปกติ
        if (freeDist >= minPushDistance)
        {
            currentSlideDirection = direction;
            remainingSlideDistance = freeDist;
            currentSlideSpeed = speed;
            PlayPushSound();
            currentSlideCoroutine = StartCoroutine(SlideRoutine(pusher));
            return true;
        }

        // 3. ถ้าติดกำแพง/สิ่งกีดขวางอยู่แล้ว และกล่องนี้ตั้งให้พังได้ และสัตว์ตัวที่ผลักมีสิทธิ์ทุบทำลาย
        if (isDestructible && pusher != null && CanAnimalDestroyBox(pusher))
        {
            // ถ้าเป็นแพะในโหมดผลักปกติ (ไม่ใช่โหมดทุบ smashMode) จะไม่ทุบกล่องแตก เว้นแต่ตั้งใจให้พังเมื่อติด (breakOnlyWhenBlocked)
            if (pusher is PusherAnimal pusherAnimal && !pusherAnimal.CanSmash && !breakOnlyWhenBlocked)
            {
                return false;
            }

            TriggerDestructionSequence(pusher, direction);
            return true;
        }

        return false;
    }

    /// <summary>วัดว่าเลื่อนไปทางนั้นได้ไกลสุดเท่าไร ก่อนจะชนสิ่งกีดขวาง</summary>
    public float GetFreeDistance(Vector2 direction, float maxDistance, TestAnimal pusher = null)
    {
        if (col == null) col = GetComponent<Collider2D>();
        if (col == null) return maxDistance;

        Bounds bounds = col.bounds;
        Vector2 origin = bounds.center;
        Vector2 size = bounds.size;

        // ดัดแปลงขนาดและตำแหน่งของกรอบ BoxCast ให้หดเข้าเล็กน้อยในแนวตั้งฉากกับทิศทางที่เลื่อน
        // เพื่อป้องกันไม่ให้ขอบล่างของกล่องไปขูด/สะดุดกับรอยต่อของพื้น หรือขูดกำแพงด้านข้างตอนตก
        if (Mathf.Abs(direction.x) > 0.5f)
        {
            // เลื่อนแนวนอน: ยกขอบล่างให้ลอยพ้นพื้น และหดด้านบนเล็กน้อย
            float insetY = Mathf.Min(0.12f, size.y * 0.25f);
            size.y = Mathf.Max(0.05f, size.y - insetY);
            size.x = Mathf.Max(0.05f, size.x - 0.02f);
            origin.y += insetY * 0.5f;
        }
        else
        {
            // เลื่อนแนวตั้ง (แรงโน้มถ่วง): บีบด้านข้างซ้าย-ขวาเข้ามาเล็กน้อยเพื่อไม่ให้ขูดกำแพงข้างๆ
            float insetX = Mathf.Min(0.12f, size.x * 0.25f);
            size.x = Mathf.Max(0.05f, size.x - insetX);
            size.y = Mathf.Max(0.05f, size.y - 0.02f);
        }

        LayerMask mask = GetEffectiveObstacleLayer();
        int count = Physics2D.BoxCastNonAlloc(origin, size, 0f, direction, castResults, maxDistance, mask);

        float free = maxDistance;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = castResults[i];
            if (hit.collider == null || hit.collider == col) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.isTrigger) continue;
            if (pusher != null && (hit.collider.gameObject == pusher.gameObject || hit.collider.transform.IsChildOf(pusher.transform))) continue;

            // ตรวจสอบ normal ของพื้นผิวสัมผัส: ต้องเป็นพื้นผิวที่ "ต้าน" ทิศทางการเคลื่อนที่เท่านั้น
            // เช่น ถ้าเลื่อนไปทางขวา (1, 0) พื้นที่มี normal (0, 1) ชี้ขึ้น จะไม่ถือเป็นสิ่งกีดขวางขวางทางเลื่อน
            if (hit.normal.sqrMagnitude > 0.001f && Vector2.Dot(direction, hit.normal) > -0.15f)
            {
                continue;
            }

            float d = Mathf.Max(0f, hit.distance - skinWidth);
            free = Mathf.Min(free, d);
        }
        return Mathf.Max(free, 0f);
    }

    private IEnumerator SlideRoutine(TestAnimal pusher = null)
    {
        isMoving = true;

        while (remainingSlideDistance > 0.001f)
        {
            float step = Mathf.Min(currentSlideSpeed * Time.fixedDeltaTime, remainingSlideDistance);
            float allowedStep = GetFreeDistance(currentSlideDirection, step, pusher);

            if (allowedStep < step * 0.4f)
            {
                remainingSlideDistance = 0f;
                break;
            }

            Vector2 gravity = GravityDelta();

            // เช็คว่าพ้นขอบพื้นตกลงไปในหลุมหรือไม่ (ไม่มีพื้นรองรับด้านล่างเกิน 0.35 หน่วย)
            // ถ้าตกลงในหลุม ให้หยุดเลื่อนแนวนอนแล้วปล่อยให้ตกตามแรงโน้มถ่วง
            float groundDist = GetFreeDistance(Vector2.down, 0.4f);
            if (Mathf.Abs(currentSlideDirection.x) > 0.5f && groundDist >= 0.35f)
            {
                ApplyMove(gravity);
                break;
            }

            ApplyMove(currentSlideDirection * allowedStep + gravity);
            remainingSlideDistance -= allowedStep;

            if (allowedStep < step)
            {
                remainingSlideDistance = 0f;
                break;
            }

            yield return new WaitForFixedUpdate();
        }

        remainingSlideDistance = 0f;
        isMoving = false;
        currentSlideCoroutine = null;
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
