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
    #endregion

    #region State
    private Rigidbody2D rb;
    private Vector2 direction;
    private float speed;
    private float range;
    private float lingerSeconds;
    private float traveled;
    private bool isFlying = true;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void FixedUpdate()
    {
        if (!isFlying) return;

        float step = speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + direction * step);
        traveled += step;

        if (traveled >= range) Land();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isFlying) return;

        if ((obstacleLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            rb.position -= direction * wallBackOff;
            Land();
        }
    }
    #endregion

    #region API
    public void Init(Vector2 fireDirection, float flySpeed, float flyRange, float linger)
    {
        direction = fireDirection.normalized;
        speed = flySpeed;
        range = flyRange;
        lingerSeconds = linger;
    }

    private void Land()
    {
        isFlying = false;
        Destroy(gameObject, lingerSeconds);
    }
    #endregion
}
