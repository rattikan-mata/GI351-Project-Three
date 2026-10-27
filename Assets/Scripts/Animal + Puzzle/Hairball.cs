using UnityEngine;

/// <summary>
/// ก้อนขนที่ HairballAnimal อ้วกออกมา: พุ่งเป็นเส้นตรง -> ตกลงพื้น -> นอนอยู่ตามเวลาที่กำหนด -> หายไป
/// ระหว่างนอนอยู่ FloorSwitch จะนับเป็นของที่ทับสวิตช์ (ถ้าสวิตช์ตั้งให้รับ Hairball)
/// ต้องมี Rigidbody2D (Kinematic) + Collider2D (ตั้ง Trigger ให้อัตโนมัติ)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Hairball : MonoBehaviour
{
    #region Settings
    [Header("Collision")]
    [Tooltip("Layer ของกำแพง/สิ่งกีดขวางที่ทำให้ก้อนขนหยุดบินทันที")]
    [SerializeField] private LayerMask obstacleLayer;

    [Tooltip("ถอยกลับจากกำแพงเล็กน้อยตอนชน กันก้อนขนจมในกำแพง")]
    [SerializeField] private float wallBackOff = 0.15f;

    [Tooltip("ติ๊กไว้ = เป็นอ้วกยักษ์ สามารถพังกล่องและประตูได้")]
    [SerializeField] private bool canBreakObstacles = false;
    public bool CanBreakObstacles { get => canBreakObstacles; set => canBreakObstacles = value; }

    [Tooltip("ติ๊กไว้ = เมื่อพังกล่อง/ประตูแล้วให้หยุดและตกลงพื้นทันที (ถ้าไม่ติ๊กจะพุ่งต่อจนสุดระยะ)")]
    [SerializeField] private bool landOnBreak = false;

    [Tooltip("ติ๊กไว้ = พังกล่องได้แม้กล่องจะไม่ถูกติ๊ก IsDestructible ไว้ใน Inspector")]
    [SerializeField] private bool forceBreakBoxes = true;
    public bool ForceBreakBoxes { get => forceBreakBoxes; set => forceBreakBoxes = value; }

    [Tooltip("ติ๊กไว้ = พังประตูได้แม้ประตูจะไม่ถูกติ๊ก IsBreakable ไว้ใน Inspector")]
    [SerializeField] private bool forceBreakDoors = true;
    public bool ForceBreakDoors { get => forceBreakDoors; set => forceBreakDoors = value; }

    [Header("Bubble Push (ฟองสบู่ดันกล่อง)")]
    [Tooltip("ติ๊กไว้ = เป็นฟองสบู่ สามารถดันกล่อง PushableBox ได้")]
    [SerializeField] private bool canPushBox = false;
    public bool CanPushBox { get => canPushBox; set => canPushBox = value; }

    [Tooltip("ระยะที่ดันกล่องไปต่อฟองสบู่ 1 ลูก (ลูกละเบาๆ)")]
    [SerializeField] private float pushBoxDistance = 0.35f;
    public float PushBoxDistance { get => pushBoxDistance; set => pushBoxDistance = value; }

    [Tooltip("ความเร็วตอนดันกล่อง")]
    [SerializeField] private float pushBoxSpeed = 3.5f;
    public float PushBoxSpeed { get => pushBoxSpeed; set => pushBoxSpeed = value; }

    [Tooltip("ติ๊กไว้ = ฟองสบู่แตกทันทีเมื่อผลักกล่องแล้ว")]
    [SerializeField] private bool destroyOnPushBox = true;
    public bool DestroyOnPushBox { get => destroyOnPushBox; set => destroyOnPushBox = value; }

    [Tooltip("ติ๊กไว้ = ฟองสบู่แตกทันทีเมื่อชนกำแพง/พื้น/เพดาน หรือหมดระยะ")]
    [SerializeField] private bool popOnWall = false;
    public bool PopOnWall { get => popOnWall; set => popOnWall = value; }

    [Tooltip("Prefab เอฟเฟกต์แตกของฟองสบู่ (Particle / Pop)")]
    [SerializeField] private GameObject popEffectPrefab;
    public GameObject PopEffectPrefab { get => popEffectPrefab; set => popEffectPrefab = value; }

    [Header("Gravity (แรงโน้มถ่วง)")]
    [Tooltip("ติ๊กไว้ = มีผลของแรงโน้มถ่วง")]
    [SerializeField] private bool useGravity = true;
    public bool UseGravity { get => useGravity; set => useGravity = value; }

    [Tooltip("ตัวคูณแรงโน้มถ่วง (1 = ปกติ)")]
    [SerializeField] private float gravityScale = 1f;
    public float GravityScale { get => gravityScale; set => gravityScale = value; }

    [Tooltip("ความเร็วตกสูงสุด")]
    [SerializeField] private float maxFallSpeed = 15f;

    [Header("Rolling & Physics (การกลิ้งและแรงสะท้อน)")]
    [Tooltip("ความฝืดตอนกลิ้งบนพื้น ยิ่งเยอะยิ่งหยุดเร็ว (หน่วย m/s^2)")]
    [SerializeField] private float groundFriction = 6f;

    [Tooltip("แรงเด้งสะท้อนเมื่อชนกำแพงหรือตกกระทบพื้น (0 = ไม่เด้งเลย, 0.8 = เด้งเยอะ)")]
    [SerializeField, Range(0f, 0.8f)] private float bounciness = 0.35f;

    [Tooltip("ความเร็วต่ำสุดที่ยังถือว่ากลิ้งอยู่ ถ้าต่ำกว่านี้จะหยุดนิ่ง")]
    [SerializeField] private float stopThreshold = 0.15f;

    [Tooltip("ติ๊กไว้ = หมุนสไปรต์ตามระยะที่กลิ้งไปอย่างสมจริง")]
    [SerializeField] private bool rotateWhenRolling = true;
    #endregion

    #region State
    private Rigidbody2D rb;
    private Vector2 direction;
    private Vector2 velocity;
    private float speed;
    private float range;
    private float lingerSeconds;
    private float traveled;
    private bool isGrounded = false;
    private float lingerTimer = 0f;

    [Header("Weight")]
    [Tooltip("มวลก้อนขน (แมวจะตั้งให้ตามชนิดการอ้วก) ใช้กับสวิตช์ที่ต้องใช้น้ำหนัก")]
    [SerializeField] private float mass = 1f;
    public float Mass { get => mass; set => mass = value; }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        GetComponent<Collider2D>().isTrigger = true;
    }

    private float GetRadius()
    {
        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        if (circle != null)
        {
            return circle.radius * scale;
        }
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            return Mathf.Max(col.bounds.extents.x, col.bounds.extents.y);
        }
        return 0.4f * scale;
    }

    /// <summary>
    /// ตรวจสอบหาพื้นรองรับข้างใต้ก้อนขน
    /// คืนค่า true ถ้ามีพื้นอยู่ใต้ก้อนขนภายในระยะ maxCheckDist พร้อมทั้งระยะห่างจริงจากขอบล่างของก้อนขนถึงพื้น (distanceToGround)
    /// </summary>
    private bool CheckGroundBelow(float maxCheckDist, out float distanceToGround, out RaycastHit2D groundHit)
    {
        float R = GetRadius();
        Vector2[] origins = new Vector2[]
        {
            rb.position,
            rb.position + Vector2.left * (R * 0.45f),
            rb.position + Vector2.right * (R * 0.45f)
        };

        float bestDist = float.MaxValue;
        RaycastHit2D bestHit = default;
        bool found = false;
        float rayLength = R + maxCheckDist;

        foreach (var origin in origins)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, rayLength);
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.gameObject == gameObject || hit.collider.isTrigger) continue;
                if (hit.collider.GetComponentInParent<TestAnimal>() != null) continue;

                PushableBox box = hit.collider.GetComponentInParent<PushableBox>();
                if (box != null && box.IsBroken) continue;

                Door door = hit.collider.GetComponentInParent<Door>();
                if (door != null && (door.IsBroken || door.IsOpen)) continue;

                bool isObstacle = ((obstacleLayer.value & (1 << hit.collider.gameObject.layer)) != 0) || box != null || door != null;
                if (isObstacle)
                {
                    float d = hit.distance - R;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestHit = hit;
                        found = true;
                    }
                }
            }
        }

        distanceToGround = found ? bestDist : float.MaxValue;
        groundHit = bestHit;
        return found;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float R = GetRadius();

        // ----------------------------------------
        // 1. จัดการความเร็วและการเคลื่อนที่แนวดิ่ง (Vertical / Gravity)
        // ----------------------------------------
        // ถ้าเปิดแรงโน้มถ่วง ให้ดึงความเร็วลง
        if (useGravity && Mathf.Abs(gravityScale) > 0.0001f)
        {
            if (!isGrounded || velocity.y > 0.001f)
            {
                velocity.y += Physics2D.gravity.y * gravityScale * dt;
                velocity.y = Mathf.Clamp(velocity.y, -maxFallSpeed, maxFallSpeed);
            }
        }

        // การเคลื่อนที่แนวดิ่ง
        if (Mathf.Abs(velocity.y) > 0.0001f)
        {
            float stepY = velocity.y * dt;

            if (stepY > 0f)
            {
                // กำลังลอยขึ้น (Skyward / เด้งขึ้น): ตรวจสอบเพดาน/กล่อง/ประตูด้านบน
                float checkDist = stepY + 0.04f;
                RaycastHit2D[] upHits = Physics2D.CircleCastAll(rb.position, R * 0.7f, Vector2.up, checkDist);
                bool hitCeiling = false;

                foreach (var hit in upHits)
                {
                    if (hit.collider == null || hit.collider.gameObject == gameObject || hit.collider.isTrigger) continue;
                    if (hit.collider.GetComponentInParent<TestAnimal>() != null) continue;

                    PushableBox box = hit.collider.GetComponentInParent<PushableBox>();
                    if (box != null && box.IsBroken) continue;

                    Door door = hit.collider.GetComponentInParent<Door>();
                    if (door != null && (door.IsBroken || door.IsOpen)) continue;

                    // ทำลายกล่อง/ประตู (สำหรับอ้วกยักษ์ และ Havoc)
                    if (canBreakObstacles)
                    {
                        if (box != null && !box.IsBroken && (box.IsDestructible || forceBreakBoxes))
                        {
                            box.BreakBox();
                            if (landOnBreak)
                            {
                                velocity.y = 0f;
                                hitCeiling = true;
                                break;
                            }
                            velocity.y *= 0.75f;
                            continue;
                        }

                        if (door != null && !door.IsBroken && (door.IsBreakable || forceBreakDoors))
                        {
                            door.BreakDoor();
                            if (landOnBreak)
                            {
                                velocity.y = 0f;
                                hitCeiling = true;
                                break;
                            }
                            velocity.y *= 0.75f;
                            continue;
                        }
                    }

                    bool isObstacle = ((obstacleLayer.value & (1 << hit.collider.gameObject.layer)) != 0) || box != null || door != null;
                    if (isObstacle)
                    {
                        // ตรวจสอบว่า Normal หันลง (เป็นเพดานขวางอยู่ข้างบน)
                        if (Vector2.Dot(hit.normal, Vector2.up) < -0.2f)
                        {
                            if (popOnWall)
                            {
                                Pop();
                                return;
                            }

                            float moveDist = Mathf.Max(0f, hit.distance - 0.02f);
                            rb.position += Vector2.up * moveDist;

                            if (useGravity && gravityScale > 0f && bounciness > 0.05f)
                            {
                                velocity.y = -velocity.y * bounciness;
                            }
                            else
                            {
                                velocity.y = 0f;
                            }

                            hitCeiling = true;
                            break;
                        }
                    }
                }

                if (!hitCeiling)
                {
                    rb.position += new Vector2(0f, stepY);
                    traveled += stepY;
                    if (traveled >= range)
                    {
                        if (popOnWall)
                        {
                            Pop();
                            return;
                        }
                        velocity.y = 0f;
                    }
                }
                isGrounded = false;
            }
            else
            {
                // กำลังร่วงลง: ตรวจสอบพื้นด้านล่าง
                float fallDist = Mathf.Abs(stepY);

                if (CheckGroundBelow(fallDist + 0.06f, out float distToGround, out RaycastHit2D gHit))
                {
                    if (distToGround <= fallDist + 0.01f)
                    {
                        if (popOnWall)
                        {
                            PushableBox groundBox = gHit.collider != null ? gHit.collider.GetComponentInParent<PushableBox>() : null;
                            if (canPushBox && groundBox != null && !groundBox.IsBroken)
                            {
                                Vector2 pushDir = new Vector2(Mathf.Sign(velocity.x != 0f ? velocity.x : direction.x), 0f);
                                if (Mathf.Abs(pushDir.x) < 0.01f) pushDir = Vector2.right;
                                groundBox.TryPush(pushDir, pushBoxDistance, pushBoxSpeed);
                            }
                            Pop();
                            return;
                        }

                        rb.position = new Vector2(rb.position.x, rb.position.y - distToGround);

                        if (bounciness > 0.15f && Mathf.Abs(velocity.y) > 2.0f)
                        {
                            velocity.y = -velocity.y * bounciness;
                            isGrounded = false;
                        }
                        else
                        {
                            velocity.y = 0f;
                            isGrounded = true;
                        }
                    }
                    else
                    {
                        rb.position += new Vector2(0f, stepY);
                        isGrounded = false;
                    }
                }
                else
                {
                    rb.position += new Vector2(0f, stepY);
                    isGrounded = false;
                }
            }
        }
        else if (useGravity && gravityScale > 0f)
        {
            // velocity.y == 0: เช็คว่ายังมีพื้นรองรับอยู่หรือไม่
            if (isGrounded)
            {
                if (CheckGroundBelow(0.12f, out float distToGround, out RaycastHit2D gHit))
                {
                    if (distToGround > 0.005f)
                    {
                        rb.position += Vector2.down * distToGround;
                    }
                    else if (distToGround < -0.005f)
                    {
                        rb.position += Vector2.up * (-distToGround);
                    }
                    isGrounded = true;
                    velocity.y = 0f;
                }
                else
                {
                    isGrounded = false;
                    velocity.y = 0f;
                }
            }
            else
            {
                isGrounded = false;
            }
        }

        // ----------------------------------------
        // 2. การเคลื่อนที่แนวนอนและการกลิ้ง (Horizontal Motion & Rolling)
        // ----------------------------------------
        if (Mathf.Abs(velocity.x) > 0.0001f)
        {
            // ถ้าอยู่บนพื้น ให้ความฝืดค่อยๆ ชะลอความเร็วจนหยุด (เหมือนของจริง)
            if (isGrounded)
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, groundFriction * dt);
                if (Mathf.Abs(velocity.x) < stopThreshold)
                {
                    velocity.x = 0f;
                }
            }

            float stepX = velocity.x * dt;

            if (Mathf.Abs(stepX) > 0.0001f)
            {
                Vector2 horizDir = stepX > 0f ? Vector2.right : Vector2.left;
                float horizDist = Mathf.Abs(stepX);

                // ตรวจจับสิ่งกีดขวางในแนวนอน (ใช้รัศมี R * 0.7f ตรงกลาง เพื่อไม่ให้ขูดโดนพื้นใต้ล้อ)
                RaycastHit2D[] horizHits = Physics2D.CircleCastAll(rb.position, R * 0.7f, horizDir, horizDist + 0.04f);
                bool hitWall = false;

                foreach (var hit in horizHits)
                {
                    if (hit.collider == null || hit.collider.gameObject == gameObject || hit.collider.isTrigger) continue;
                    if (hit.collider.GetComponentInParent<TestAnimal>() != null) continue;

                    PushableBox box = hit.collider.GetComponentInParent<PushableBox>();
                    if (box != null && box.IsBroken) continue;

                    Door door = hit.collider.GetComponentInParent<Door>();
                    if (door != null && (door.IsBroken || door.IsOpen)) continue;

                    // ทำลายกล่อง / ประตู (สำหรับอ้วกยักษ์ และ Havoc)
                    if (canBreakObstacles)
                    {
                        if (box != null && !box.IsBroken && (box.IsDestructible || forceBreakBoxes))
                        {
                            Debug.Log($"[Hairball] ชนกล่อง {box.name} แตก!");
                            box.BreakBox();
                            if (landOnBreak)
                            {
                                velocity.x = 0f;
                                hitWall = true;
                                break;
                            }
                            velocity.x *= 0.75f; // ชนแตกแล้วกลิ้งทะลุผ่านต่อด้วยความเร็วที่ลดลงเล็กน้อย
                            continue;
                        }

                        if (door != null && !door.IsBroken && (door.IsBreakable || forceBreakDoors))
                        {
                            Debug.Log($"[Hairball] ชนประตู {door.name} พัง!");
                            door.BreakDoor();
                            if (landOnBreak)
                            {
                                velocity.x = 0f;
                                hitWall = true;
                                break;
                            }
                            velocity.x *= 0.75f;
                            continue;
                        }
                    }

                    // ฟองสบู่ผลักกล่อง
                    if (canPushBox && box != null && !box.IsBroken)
                    {
                        box.TryPush(horizDir, pushBoxDistance, pushBoxSpeed);
                        if (destroyOnPushBox)
                        {
                            Pop();
                            return;
                        }
                    }

                    // สิ่งกีดขวางที่ชนแล้วไปต่อไม่ได้ (กำแพง, กล่องตัน, ประตูตัน)
                    bool isObstacle = ((obstacleLayer.value & (1 << hit.collider.gameObject.layer)) != 0) || box != null || door != null;
                    if (isObstacle)
                    {
                        // ตรวจสอบว่า Normal หันต้านทิศที่เราวิ่งไป (เป็นกำแพงขวางหน้า)
                        if (Vector2.Dot(hit.normal, horizDir) < -0.2f)
                        {
                            if (popOnWall)
                            {
                                Pop();
                                return;
                            }

                            float moveDist = Mathf.Max(0f, hit.distance - 0.02f);
                            rb.position += horizDir * moveDist;

                            // เด้งสะท้อนกลับตามโลกจริง (Bounce off wall)
                            if (bounciness > 0.05f && Mathf.Abs(velocity.x) > stopThreshold)
                            {
                                velocity.x = -velocity.x * bounciness;
                                if (Mathf.Abs(velocity.x) < stopThreshold) velocity.x = 0f;
                            }
                            else
                            {
                                velocity.x = 0f;
                                rb.position -= horizDir * wallBackOff;
                            }

                            hitWall = true;
                            break;
                        }
                    }
                }

                if (!hitWall)
                {
                    rb.position += new Vector2(stepX, 0f);
                    traveled += horizDist;

                    if (traveled >= range)
                    {
                        if (popOnWall)
                        {
                            Pop();
                            return;
                        }
                        velocity.x = 0f;
                    }

                    // หมุนสไปรต์ตามระยะทางที่กลิ้งไปจริง (เหมือนลูกบอลกลิ้งในโลกจริง)
                    if (rotateWhenRolling)
                    {
                        float angleDelta = -(stepX / Mathf.Max(0.05f, R)) * Mathf.Rad2Deg;
                        transform.Rotate(0f, 0f, angleDelta);
                    }
                }
            }
        }

        // ----------------------------------------
        // 3. จัดการการหมดอายุ (Linger Countdown เมื่อหยุดนิ่งแล้ว)
        // ----------------------------------------
        bool isMoving = Mathf.Abs(velocity.x) > 0.01f || Mathf.Abs(velocity.y) > 0.01f || !isGrounded;
        if (!isMoving)
        {
            lingerTimer += dt;
            if (lingerTimer >= lingerSeconds)
            {
                Pop();
                return;
            }
        }
        else
        {
            lingerTimer = 0f;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || other.gameObject == gameObject) return;
        if (other.isTrigger) return;
        if (other.GetComponentInParent<TestAnimal>() != null) return;

        PushableBox box = other.GetComponentInParent<PushableBox>();
        if (box != null && box.IsBroken) return;

        // ฟองสบู่ผลักกล่อง
        if (canPushBox && box != null && !box.IsBroken)
        {
            Vector2 pushDir = new Vector2(Mathf.Sign(velocity.x != 0f ? velocity.x : direction.x), 0f);
            if (Mathf.Abs(pushDir.x) < 0.01f) pushDir = Vector2.right;
            box.TryPush(pushDir, pushBoxDistance, pushBoxSpeed);
            if (destroyOnPushBox)
            {
                Pop();
                return;
            }
        }

        Door door = other.GetComponentInParent<Door>();
        if (door != null && (door.IsBroken || door.IsOpen)) return;

        if (canBreakObstacles)
        {
            if (box != null && !box.IsBroken && (box.IsDestructible || forceBreakBoxes))
            {
                Debug.Log($"[Hairball] ชนกล่อง {box.name} แตก!");
                box.BreakBox();
                if (landOnBreak)
                {
                    velocity = Vector2.zero;
                    return;
                }
                velocity.x *= 0.75f;
                velocity.y *= 0.75f;
                return;
            }

            if (door != null && !door.IsBroken && (door.IsBreakable || forceBreakDoors))
            {
                Debug.Log($"[Hairball] ชนประตู {door.name} พัง!");
                door.BreakDoor();
                if (landOnBreak)
                {
                    velocity = Vector2.zero;
                    return;
                }
                velocity.x *= 0.75f;
                velocity.y *= 0.75f;
                return;
            }
        }

        bool isObstacle = ((obstacleLayer.value & (1 << other.gameObject.layer)) != 0) || box != null || door != null;
        if (isObstacle)
        {
            if (popOnWall)
            {
                Pop();
                return;
            }
            float R = GetRadius();
            // 1. ตรวจสอบว่าชนพื้นด้านล่าง
            if (other.bounds.max.y <= rb.position.y - R * 0.4f)
            {
                // ชนพื้น: วางบนพื้นพอดี
                rb.position = new Vector2(rb.position.x, other.bounds.max.y + R);
                velocity.y = 0f;
                isGrounded = true;
                return;
            }

            // 2. ตรวจสอบว่าชนเพดานด้านบน (Skyward หรือตอนเด้งขึ้น)
            if (other.bounds.min.y >= rb.position.y + R * 0.4f)
            {
                // ชนเพดาน: วางใต้เพดานพอดี
                rb.position = new Vector2(rb.position.x, other.bounds.min.y - R);
                if (useGravity && gravityScale > 0f && bounciness > 0.05f)
                {
                    velocity.y = -velocity.y * bounciness;
                }
                else
                {
                    velocity.y = 0f;
                }
                return;
            }

            // 3. ชนกำแพงด้านข้าง: เด้งสะท้อนกลับ
            if (bounciness > 0.05f && Mathf.Abs(velocity.x) > stopThreshold)
            {
                velocity.x = -velocity.x * bounciness;
            }
            else
            {
                velocity.x = 0f;
                Vector2 backDir = velocity.sqrMagnitude > 0.01f ? velocity.normalized : direction;
                rb.position -= backDir * wallBackOff;
            }
        }
    }
    #endregion

    #region API
    public void SetupBubblePush(bool canPush, float pushDist, float pushSpeed, bool destroyOnPush = true, bool popOnObstacle = true, GameObject popPrefab = null)
    {
        canPushBox = canPush;
        pushBoxDistance = pushDist;
        pushBoxSpeed = pushSpeed;
        destroyOnPushBox = destroyOnPush;
        popOnWall = popOnObstacle;
        popEffectPrefab = popPrefab;
    }

    public void Pop()
    {
        if (popEffectPrefab != null)
        {
            Instantiate(popEffectPrefab, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }

    public void Init(Vector2 fireDirection, float flySpeed, float flyRange, float linger, float gravScale = 1f)
    {
        direction = fireDirection.normalized;
        speed = flySpeed;
        range = flyRange;
        lingerSeconds = linger;
        gravityScale = gravScale;
        useGravity = Mathf.Abs(gravScale) > 0.0001f;
        velocity = direction * speed;
        isGrounded = false;
        traveled = 0f;
        lingerTimer = 0f;
    }

    public void Land()
    {
        velocity = Vector2.zero;
        isGrounded = true;
    }

    /// <summary>ดีดก้อนขนด้วยความเร็วที่กำหนด (เช่น เทรมโพลีน) แล้วให้แรงโน้มถ่วง/การกลิ้งทำงานต่อ</summary>
    public void Launch(Vector2 newVelocity)
    {
        velocity = newVelocity;
        isGrounded = false;
        traveled = 0f;
        range = Mathf.Max(range, 20f);
        lingerTimer = 0f;
    }
    #endregion
}
