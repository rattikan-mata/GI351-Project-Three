using System;
using UnityEngine;

/// <summary>
/// ยาที่ถูกปล่อยออกมาจาก PotionDropper: ลอยผ่านจุด Path (ถ้ามี) แล้วเข้าไปหาสัตว์
/// พอถึงตัวสัตว์จะเรียก DrinkPotion() ถ้าเป็นยาที่ถูกตัว สัตว์จะเริ่มแก้ puzzle เอง
/// ถ้าเป็นยาผิดตัว สัตว์ไม่ทำอะไร ยาหายไป (ยาเสียเปล่า)
/// ไม่ต้องมี Collider/Rigidbody (ขยับด้วย Transform)
/// </summary>
public class PotionProjectile : MonoBehaviour
{
    [Serializable]
    public class PotionColor
    {
        public PotionType type;
        public Color color = Color.white;
    }

    #region Settings
    [Header("Settings")]
    [Tooltip("ห่างจากเป้าหมายเท่านี้ถือว่าถึงแล้ว")]
    [SerializeField] private float arriveDistance = 0.3f;

    [Header("Visual (Optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("สีของยาแต่ละชนิด (ไม่บังคับ ใช้ย้อมสไปรต์ให้แยกชนิดออก)")]
    [SerializeField] private PotionColor[] potionColors;
    #endregion

    #region State
    private PotionType potionType;
    private TestAnimal target;
    private Transform[] path;
    private int pathIndex = 0;
    private float speed;
    private bool initialized = false;
    private bool faceMovement = false;
    private float faceAngleOffset = 0f;
    #endregion

    #region API

    public void Init(PotionType type, TestAnimal targetAnimal, Transform[] pathPoints, float moveSpeed)
    {
        potionType = type;
        target = targetAnimal;
        path = pathPoints;
        speed = moveSpeed;
        initialized = true;

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && potionColors != null)
        {
            foreach (var pc in potionColors)
            {
                if (pc.type == type) { spriteRenderer.color = pc.color; break; }
            }
        }
    }

    /// <summary>ใช้กับเข็มของ DartGun: หันหัวเข็มไปตามทิศที่บิน (spriteForwardAngle = ทิศปลายเข็มในรูปที่ยังไม่หมุน)</summary>
    public void ConfigureDart(float spriteForwardAngle, float arrive)
    {
        faceMovement = true;
        faceAngleOffset = spriteForwardAngle;
        arriveDistance = arrive;
        FaceTo(target != null ? target.transform.position : transform.position);
    }

    private void FaceTo(Vector3 dest)
    {
        Vector2 dir = dest - transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        float z = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - faceAngleOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, z);
    }
    #endregion

    #region Unity Lifecycle
    private void Update()
    {
        if (!initialized) return;

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // ข้ามจุด path ที่ถูกลบทิ้งไปแล้ว
        while (path != null && pathIndex < path.Length && path[pathIndex] == null) pathIndex++;

        bool onWaypoint = path != null && pathIndex < path.Length;
        Vector3 dest = onWaypoint ? path[pathIndex].position : target.transform.position;

        if (faceMovement) FaceTo(dest);
        transform.position = Vector3.MoveTowards(transform.position, dest, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, dest) > arriveDistance) return;

        if (onWaypoint)
        {
            pathIndex++;
            return;
        }

        Arrive();
    }
    #endregion

    #region Logic
    private void Arrive()
    {
        bool accepted = target.DrinkPotion(potionType);
        if (!accepted)
        {
            Debug.Log($"[Potion] ยา {potionType} ไม่ตรงกับ {target.AnimalName} (ต้องใช้ {target.RequiredPotionType}) ยาเสียเปล่า");
        }
        Destroy(gameObject);
    }
    #endregion
}
