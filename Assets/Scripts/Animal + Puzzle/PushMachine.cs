using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// เครื่องผลัก: ผลักกล่อง / สัตว์ / projectile (ก้อนขน, ฟองสบู่) ออกไปตามทิศทางและความเร็วที่ตั้ง
/// สามารถปรับ ระยะผลัก (pushDistance), ความกว้าง (pushWidth) และทิศทางได้ใน Inspector
/// ใน Scene view จะมี Gizmo แสดงกรอบพื้นที่ผลัก ระยะทาง (เมตร) ขีดสเกลวัดระยะ และลูกศรบอกทิศอย่างชัดเจนตลอดเวลา
/// ถ้าลาก FloorSwitch ใส่ช่อง Switches เครื่องจะทำงานตามสวิตช์ ถ้าไม่ใส่เลยจะทำงานตลอดเวลา
/// </summary>
public class PushMachine : MonoBehaviour
{
    #region Switch Link
    [Header("Switch Link (สวิตช์ที่เปิดเครื่องผลัก)")]
    [Tooltip("ลาก FloorSwitch จากฉากมาใส่ ว่างไว้ = เครื่องทำงานตลอด")]
    [SerializeField] private FloorSwitch[] switches;

    [Tooltip("ติ๊กไว้ = ต้องกดสวิตช์ครบทุกตัวพร้อมกัน / ไม่ติ๊ก = ตัวใดตัวหนึ่งก็พอ")]
    [SerializeField] private bool requireAll = true;

    [Tooltip("ติ๊กไว้ = เปิดแล้วทำงานต่อเนื่อง แม้สวิตช์จะปล่อยแล้ว")]
    [SerializeField] private bool stayOnOnceActivated = false;
    #endregion

    #region Push Shape & Force
    [Header("Push Shape & Force (ทิศทาง ขนาด และระยะผลัก)")]
    [Tooltip("ทิศที่ผลักของออกไป")]
    [SerializeField] private AutoDirection pushDirection = AutoDirection.Right;

    [Tooltip("หมุนทิศผลักเพิ่มอีกกี่องศา (ทวนเข็มนาฬิกา) ใช้ทำทิศเฉียงแบบอิสระ")]
    [SerializeField] private float extraAngle = 0f;

    [Tooltip("ระยะห่างจากตัวเครื่องถึงจุดที่เริ่มผลัก (หน่วย Unity)")]
    [SerializeField, Min(0f)] private float startOffset = 0f;

    [Tooltip("ระยะที่เครื่องผลักไปได้ไกลสุด (หน่วย Unity) แสดงใน Gizmo ให้เห็น")]
    [SerializeField, Min(0.1f)] private float pushDistance = 3f;

    [Tooltip("ความกว้างของพื้นที่ผลัก (หน่วย Unity)")]
    [SerializeField, Min(0.1f)] private float pushWidth = 1f;

    [Tooltip("ความเร็วที่ผลักของไป (หน่วยต่อวินาที)")]
    [SerializeField, Min(0f)] private float pushSpeed = 3.5f;

    [Tooltip("Layer สิ่งกีดขวาง (กำแพง ประตู) ที่ขวางแรงผลัก ถ้าติ๊กไว้ แรงผลักจะหยุดที่กำแพง")]
    [SerializeField] private LayerMask obstacleLayer;

    [SerializeField] private float skinWidth = 0.02f;
    #endregion

    #region Who Is Pushed
    [Header("Who Is Pushed (อะไรถูกผลักได้)")]
    [SerializeField] private bool pushBoxes = true;
    [SerializeField] private bool pushAnimals = true;
    [Tooltip("projectile ของสัตว์ เช่น ก้อนขน / ฟองสบู่")]
    [SerializeField] private bool pushProjectiles = true;

    [Tooltip("ติ๊กไว้ = สัตว์ที่กำลังบินอยู่จะไม่ถูกผลัก")]
    [SerializeField] private bool ignoreAirborneAnimals = true;
    #endregion

    #region Visual
    [Header("Visual (Optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite offSprite;
    [SerializeField] private Sprite onSprite;
    [SerializeField] private Animator animator;
    [SerializeField] private string onBoolParameter = "On";
    #endregion

    #region State
    public bool IsPowered { get; private set; }

    public float PushDistance
    {
        get => pushDistance;
        set => pushDistance = Mathf.Max(0.1f, value);
    }

    public float PushWidth
    {
        get => pushWidth;
        set => pushWidth = Mathf.Max(0.1f, value);
    }

    public float PushSpeed
    {
        get => pushSpeed;
        set => pushSpeed = Mathf.Max(0f, value);
    }

    private readonly Collider2D[] hitBuffer = new Collider2D[32];
    private readonly HashSet<GameObject> currentFrameOccupants = new HashSet<GameObject>();
    private readonly HashSet<TestAnimal> previousAnimals = new HashSet<TestAnimal>();
    private readonly RaycastHit2D[] castResults = new RaycastHit2D[8];
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (switches == null) return;
        foreach (var sw in switches)
        {
            if (sw != null) sw.StateChanged += OnSwitchChanged;
        }
    }

    private void OnDisable()
    {
        if (switches != null)
        {
            foreach (var sw in switches)
            {
                if (sw != null) sw.StateChanged -= OnSwitchChanged;
            }
        }
        ClearAnimalPush();
        previousAnimals.Clear();
        currentFrameOccupants.Clear();
    }

    private void Start()
    {
        Evaluate();
        ApplyVisual();
    }

    private void FixedUpdate()
    {
        Evaluate();

        if (!IsPowered)
        {
            ClearAnimalPush();
            return;
        }

        GetPushBox(out Vector2 center, out Vector2 size, out float angle, out float effDist);
        Vector2 dir = GetPushVector();
        float step = pushSpeed * Time.fixedDeltaTime;

        int count = Physics2D.OverlapBoxNonAlloc(center, size, angle, hitBuffer);
        currentFrameOccupants.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider2D col = hitBuffer[i];
            if (col == null || col.transform.IsChildOf(transform)) continue;

            // 1. สัตว์ (TestAnimal)
            var animal = col.GetComponentInParent<TestAnimal>();
            if (animal != null)
            {
                if (pushAnimals && !(ignoreAirborneAnimals && animal.IsAirborne))
                {
                    animal.ConveyorVelocity = dir * pushSpeed;
                    currentFrameOccupants.Add(animal.gameObject);
                }
                continue;
            }

            // 2. กล่อง (PushableBox)
            var box = col.GetComponentInParent<PushableBox>();
            if (box != null)
            {
                if (pushBoxes && !box.IsBroken)
                {
                    box.TryPush(dir, step * 2.5f, pushSpeed);
                }
                continue;
            }

            // 3. ก้อนขน / ฟองสบู่ (Hairball)
            if (pushProjectiles)
            {
                var hairball = col.GetComponentInParent<Hairball>();
                if (hairball != null)
                {
                    var rb = col.attachedRigidbody != null ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody2D>();
                    if (rb != null) MoveKinematic(rb, col, dir, step);
                }
            }
        }

        // รีเซ็ตความเร็วของสัตว์ที่หลุดออกจากระยะผลัก
        previousAnimals.RemoveWhere(a =>
        {
            if (a == null) return true;
            if (!currentFrameOccupants.Contains(a.gameObject))
            {
                a.ConveyorVelocity = Vector2.zero;
                return true;
            }
            return false;
        });

        foreach (var go in currentFrameOccupants)
        {
            if (go != null)
            {
                var a = go.GetComponentInParent<TestAnimal>();
                if (a != null) previousAnimals.Add(a);
            }
        }
    }
    #endregion

    #region Logic
    private void OnSwitchChanged(FloorSwitch changed) => Evaluate();

    private void Evaluate()
    {
        if (IsPowered && stayOnOnceActivated) return;

        bool shouldRun = ShouldRun();
        if (shouldRun == IsPowered) return;

        IsPowered = shouldRun;
        ApplyVisual();
        if (!IsPowered) ClearAnimalPush();
    }

    private bool ShouldRun()
    {
        if (switches == null || switches.Length == 0) return true;

        bool any = false;
        bool all = true;
        bool hasAny = false;
        foreach (var sw in switches)
        {
            if (sw == null) continue;
            hasAny = true;
            if (sw.IsActive) any = true;
            else all = false;
        }
        if (!hasAny) return true;
        return requireAll ? all : any;
    }

    private void MoveKinematic(Rigidbody2D rb, Collider2D col, Vector2 dir, float distance)
    {
        float free = GetFreeDistance(col, dir, distance);
        if (free <= 0f) return;
        rb.MovePosition(rb.position + dir * free);
    }

    private float GetFreeDistance(Collider2D col, Vector2 direction, float maxDistance)
    {
        if (obstacleLayer.value == 0) return maxDistance;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleLayer);
        filter.useTriggers = false;

        int count = col.Cast(direction, filter, castResults, maxDistance);
        float free = maxDistance;
        for (int i = 0; i < count; i++)
            free = Mathf.Min(free, castResults[i].distance - skinWidth);
        return Mathf.Max(free, 0f);
    }

    private void ClearAnimalPush()
    {
        foreach (var a in previousAnimals)
        {
            if (a != null) a.ConveyorVelocity = Vector2.zero;
        }
        previousAnimals.Clear();
    }

    private void ApplyVisual()
    {
        if (spriteRenderer != null)
        {
            Sprite s = IsPowered ? onSprite : offSprite;
            if (s != null) spriteRenderer.sprite = s;
        }

        if (animator != null && !string.IsNullOrEmpty(onBoolParameter))
            animator.SetBool(onBoolParameter, IsPowered);
    }
    #endregion

    #region Geometry
    public Vector2 GetPushVector()
    {
        Vector2 v = ToDirection(pushDirection);
        return Quaternion.Euler(0f, 0f, extraAngle) * v;
    }

    public float GetEffectiveDistance(Vector2 origin, Vector2 dir, float maxDist)
    {
        if (obstacleLayer.value == 0) return maxDist;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, maxDist, obstacleLayer);
        float best = maxDist;
        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.isTrigger || h.collider.transform.IsChildOf(transform)) continue;
            best = Mathf.Min(best, h.distance);
        }
        return Mathf.Max(best, 0.05f);
    }

    public void GetPushBox(out Vector2 center, out Vector2 size, out float angle, out float effDist)
    {
        Vector2 dir = GetPushVector();
        Vector2 origin = (Vector2)transform.position + dir * startOffset;
        effDist = GetEffectiveDistance(origin, dir, pushDistance);

        center = origin + dir * (effDist * 0.5f);
        size = new Vector2(effDist, pushWidth);
        angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }

    private static Vector2 ToDirection(AutoDirection d)
    {
        switch (d)
        {
            case AutoDirection.Right: return Vector2.right;
            case AutoDirection.Left: return Vector2.left;
            case AutoDirection.Up: return Vector2.up;
            case AutoDirection.Down: return Vector2.down;
            case AutoDirection.UpRight: return new Vector2(1f, 1f).normalized;
            case AutoDirection.UpLeft: return new Vector2(-1f, 1f).normalized;
            case AutoDirection.DownRight: return new Vector2(1f, -1f).normalized;
            default: return new Vector2(-1f, -1f).normalized;
        }
    }
    #endregion

    #region Debug
    private void OnDrawGizmos()
    {
        Vector2 dir = GetPushVector();
        Vector2 origin = (Vector2)transform.position + dir * startOffset;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector2 normal = new Vector2(-dir.y, dir.x);

        // 1. วาดกรอบสี่เหลี่ยมเต็มระยะ (Max Distance ที่ตั้งไว้ใน Inspector)
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Vector3 fullCenter = origin + dir * (pushDistance * 0.5f);
        Gizmos.matrix = Matrix4x4.TRS(fullCenter, Quaternion.Euler(0f, 0f, angle), Vector3.one);

        bool live = Application.isPlaying && IsPowered;
        Gizmos.color = live ? new Color(0.2f, 0.85f, 1f, 0.25f) : new Color(1f, 0.65f, 0.15f, 0.18f);
        Gizmos.DrawCube(Vector3.zero, new Vector3(pushDistance, pushWidth, 0.01f));
        Gizmos.color = live ? new Color(0.2f, 0.85f, 1f, 0.95f) : new Color(1f, 0.65f, 0.15f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(pushDistance, pushWidth, 0.01f));
        Gizmos.matrix = oldMatrix;

        // 2. ถ้ามีกำแพงขวาง (Effective Distance สั้นกว่า) วาดกรอบสีเหลืองแสดงระยะจริงที่ผลักถึง
        float effDist = Application.isPlaying || obstacleLayer.value != 0
            ? GetEffectiveDistance(origin, dir, pushDistance)
            : pushDistance;

        if (effDist < pushDistance - 0.01f)
        {
            Gizmos.matrix = Matrix4x4.TRS(origin + dir * (effDist * 0.5f), Quaternion.Euler(0f, 0f, angle), Vector3.one);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(effDist, pushWidth, 0.01f));
            Gizmos.matrix = oldMatrix;
        }

        // 3. เส้นแกนกลาง + จุดเริ่มต้น + ขีดบอกระยะทุก 1 เมตร (Ruler Ticks)
        Vector3 vOrigin = origin;
        Vector3 vDir = dir;
        Vector3 vNorm = normal;

        Gizmos.color = new Color(1f, 0.85f, 0.3f, 1f);
        Gizmos.DrawLine(transform.position, vOrigin);
        Gizmos.DrawWireSphere(vOrigin, 0.08f);

        Vector3 tip = vOrigin + vDir * effDist;
        Gizmos.DrawLine(vOrigin, tip);

        // หัวลูกศร
        Vector3 arrowSide = vNorm * 0.22f;
        Gizmos.DrawLine(tip, tip - vDir * 0.35f + arrowSide);
        Gizmos.DrawLine(tip, tip - vDir * 0.35f - arrowSide);

        // ขีดบอกระยะทีละ 1 เมตรตามแนวเส้น (Meter Ticks)
        float tickHalfWidth = Mathf.Min(0.2f, pushWidth * 0.25f);
        for (float m = 1f; m < effDist; m += 1f)
        {
            Vector3 tickPos = vOrigin + vDir * m;
            Gizmos.DrawLine(tickPos - vNorm * tickHalfWidth, tickPos + vNorm * tickHalfWidth);
        }

        // เส้นปิดท้ายระยะผลัก (End Cap)
        Vector3 endCapA = tip + vNorm * (pushWidth * 0.5f);
        Vector3 endCapB = tip - vNorm * (pushWidth * 0.5f);
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.85f);
        Gizmos.DrawLine(endCapA, endCapB);

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(1f, 0.9f, 0.25f, 1f);
        string labelText = effDist < pushDistance - 0.01f
            ? $"Push Dist: {effDist:0.0}m (Blocked / Max {pushDistance:0.0}m) | Speed: {pushSpeed:0.0}m/s"
            : $"Push Dist: {pushDistance:0.0}m | Speed: {pushSpeed:0.0}m/s";

        Vector3 labelPos = fullCenter + vNorm * (pushWidth * 0.5f + 0.2f);
        UnityEditor.Handles.Label(labelPos, labelText);

        // แสดงตัวเลขเมตรบนแต่ละขีดสเกล
        if (pushDistance <= 15f)
        {
            GUIStyle smallStyle = new GUIStyle(UnityEditor.EditorStyles.miniLabel);
            smallStyle.normal.textColor = new Color(1f, 0.85f, 0.3f, 0.9f);
            for (float m = 1f; m < effDist; m += 1f)
            {
                Vector3 tickPos = vOrigin + vDir * m + vNorm * (tickHalfWidth + 0.05f);
                UnityEditor.Handles.Label(tickPos, $"{m:0}m", smallStyle);
            }
        }
#endif
    }
    #endregion
}
