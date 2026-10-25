using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// เครื่องพ่นไฟ: พ่นไฟออกไปตามทิศที่ตั้ง ปรับ ความยาว (ระยะ) / ความกว้าง ได้ใน Inspector
/// พื้นที่ไฟแสดงเป็นกรอบสี่เหลี่ยมใน Scene view (Gizmo) ตลอดเวลา ไม่ต้องเลือกวัตถุ
/// ทำงานตามสวิตช์ (ถ้าใส่) และ/หรือ เปิด-ปิดสลับอัตโนมัติตามเวลาได้
/// ไฟจะทำลายกล่อง / ประตูที่พังได้ / ส่ง Event เมื่อโดนสัตว์ (ต่อกับระบบรีเซ็ตด่านได้)
/// ไม่ต้องมี Collider ก็ใช้งานได้ (ใช้การตรวจพื้นที่ด้วยโค้ด)
/// </summary>
public class FlameThrower : MonoBehaviour
{
    #region Flame Shape
    [Header("Flame Shape (ทิศ ระยะ และขนาดไฟ)")]
    [Tooltip("ทิศที่พ่นไฟออกไป")]
    [SerializeField] private AutoDirection fireDirection = AutoDirection.Right;

    [Tooltip("หมุนทิศพ่นเพิ่มอีกกี่องศา (ทวนเข็มนาฬิกา) ใช้ทำทิศเฉียงแบบอิสระ")]
    [SerializeField] private float extraAngle = 0f;

    [Tooltip("ระยะห่างจากจุดวางเครื่องถึงจุดที่ไฟเริ่ม (หน่วย Unity)")]
    [SerializeField, Min(0f)] private float startOffset = 0.3f;

    [Tooltip("ความยาวของไฟ = ระยะที่พ่นไปไกลสุด (หน่วย Unity)")]
    [SerializeField, Min(0.1f)] private float flameLength = 3f;

    [Tooltip("ความกว้างของไฟ (หน่วย Unity)")]
    [SerializeField, Min(0.1f)] private float flameWidth = 1f;

    [Tooltip("Layer กำแพง/สิ่งกีดขวางที่ขวางไฟ ถ้าติ๊กเลือกไว้ ไฟจะสั้นลงเมื่อชนกำแพง เว้นว่าง = ไฟทะลุทุกอย่าง")]
    [SerializeField] private LayerMask blockLayer;
    #endregion

    #region Timing
    [Header("Switch Link (สวิตช์ที่เปิดเครื่องพ่นไฟ)")]
    [Tooltip("ลาก FloorSwitch จากฉากมาใส่ ว่างไว้ = ไม่ผูกกับสวิตช์")]
    [SerializeField] private FloorSwitch[] switches;

    [Tooltip("ติ๊กไว้ = ต้องกดสวิตช์ครบทุกตัวพร้อมกัน / ไม่ติ๊ก = ตัวใดตัวหนึ่งก็พอ")]
    [SerializeField] private bool requireAll = true;

    [Tooltip("ติ๊กไว้ = สวิตช์ทำงานแล้วไฟดับ (กลับด้านสวิตช์ ใช้ทำสวิตช์ดับไฟ)")]
    [SerializeField] private bool invertSwitch = false;

    [Header("Auto Cycle (เปิด-ปิดสลับอัตโนมัติ)")]
    [Tooltip("ติ๊กไว้ = ไฟติด-ดับสลับกันตามเวลาด้านล่าง")]
    [SerializeField] private bool autoCycle = false;

    [SerializeField, Min(0.1f)] private float onDuration = 2f;
    [SerializeField, Min(0.1f)] private float offDuration = 2f;

    [Tooltip("เลื่อนจังหวะเริ่มต้น (วินาที) ใช้ทำให้เครื่องหลายตัวไม่ติดพร้อมกัน")]
    [SerializeField, Min(0f)] private float cycleStartDelay = 0f;
    #endregion

    #region Effects
    [Header("Burn Effects (ไฟทำอะไรได้บ้าง)")]
    [Tooltip("ไฟทำให้กล่อง (PushableBox) พังทันที")]
    [SerializeField] private bool burnBoxes = true;

    [Tooltip("ไฟทำให้ประตูที่ติ๊ก Breakable ไว้พัง")]
    [SerializeField] private bool burnDoors = false;

    [Tooltip("ติ๊กไว้ = เผาประตูทุกบานแม้ไม่ได้ติ๊ก Breakable")]
    [SerializeField] private bool forceBurnDoors = false;

    [Tooltip("ตรวจเจอสัตว์ในไฟ (ใช้ส่ง Event / รีเซ็ตด่าน)")]
    [SerializeField] private bool detectAnimals = true;

    [Tooltip("ติ๊กไว้ = สัตว์โดนไฟแล้วสั่งรีเซ็ตด่านด้วย PuzzleResetManager ในฉาก")]
    [SerializeField] private bool resetPuzzleOnAnimalBurn = false;

    [Tooltip("เวลาพักก่อนสัตว์ตัวเดิมโดนไฟซ้ำและส่ง Event ได้อีก (วินาที)")]
    [SerializeField, Min(0f)] private float animalBurnCooldown = 1f;

    public UnityEvent<TestAnimal> onAnimalBurned;
    public UnityEvent<PushableBox> onBoxBurned;
    #endregion

    #region Visual
    [Header("Visual (Optional)")]
    [Tooltip("(ไม่บังคับ) Particle ไฟ จะถูกเปิด/ปิดตามสถานะไฟ")]
    [SerializeField] private ParticleSystem flameParticles;

    [Tooltip("(ไม่บังคับ) วัตถุภาพไฟ จะถูกเปิด/ปิดตามสถานะไฟ")]
    [SerializeField] private GameObject flameVisual;

    [SerializeField] private AudioSource loopSound;
    #endregion

    #region State
    public bool IsFiring { get; private set; }
    public float FlameLength { get => flameLength; set => flameLength = Mathf.Max(0.1f, value); }
    public float FlameWidth { get => flameWidth; set => flameWidth = Mathf.Max(0.1f, value); }

    private bool manualOverride = false;
    private bool manualState = false;
    private float cycleTimer;
    private bool cycleOn = true;

    private readonly Collider2D[] hitBuffer = new Collider2D[32];
    private readonly System.Collections.Generic.Dictionary<TestAnimal, float> lastBurn =
        new System.Collections.Generic.Dictionary<TestAnimal, float>();
    #endregion

    #region Unity Lifecycle
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
        SetFiring(false);
    }

    private void Start()
    {
        cycleOn = true;
        cycleTimer = -cycleStartDelay;
        Evaluate();
    }

    private void FixedUpdate()
    {
        if (autoCycle) UpdateCycle();
        Evaluate();

        if (IsFiring) Burn();
    }
    #endregion

    #region Logic
    private void OnSwitchChanged(FloorSwitch changed) => Evaluate();

    /// <summary>บังคับเปิด/ปิดไฟจากสคริปต์อื่น (ทับสวิตช์และตัวตั้งเวลา) เรียก ClearManualOverride() เพื่อกลับสู่ปกติ</summary>
    public void SetManual(bool on)
    {
        manualOverride = true;
        manualState = on;
        Evaluate();
    }

    public void ClearManualOverride()
    {
        manualOverride = false;
        Evaluate();
    }

    private void UpdateCycle()
    {
        cycleTimer += Time.fixedDeltaTime;
        if (cycleTimer < 0f) return;

        float limit = cycleOn ? onDuration : offDuration;
        if (cycleTimer >= limit)
        {
            cycleTimer = 0f;
            cycleOn = !cycleOn;
        }
    }

    private void Evaluate()
    {
        bool shouldFire;
        if (manualOverride)
        {
            shouldFire = manualState;
        }
        else
        {
            shouldFire = true;
            if (HasSwitches())
            {
                bool sw = SwitchesSatisfied();
                shouldFire = invertSwitch ? !sw : sw;
            }
            if (autoCycle && shouldFire) shouldFire = cycleOn && cycleTimer >= 0f;
            else if (autoCycle) shouldFire = false;
        }

        SetFiring(shouldFire);
    }

    private bool HasSwitches()
    {
        if (switches == null) return false;
        foreach (var sw in switches)
        {
            if (sw != null) return true;
        }
        return false;
    }

    private bool SwitchesSatisfied()
    {
        bool any = false;
        bool all = true;
        foreach (var sw in switches)
        {
            if (sw == null) continue;
            if (sw.IsActive) any = true;
            else all = false;
        }
        return requireAll ? all : any;
    }

    private void SetFiring(bool firing)
    {
        if (IsFiring == firing && Application.isPlaying && (flameVisual == null || flameVisual.activeSelf == firing)) return;
        IsFiring = firing;

        if (flameVisual != null) flameVisual.SetActive(firing);
        if (flameParticles != null)
        {
            if (firing && !flameParticles.isPlaying) flameParticles.Play();
            else if (!firing && flameParticles.isPlaying) flameParticles.Stop();
        }
        if (loopSound != null)
        {
            if (firing && !loopSound.isPlaying) loopSound.Play();
            else if (!firing && loopSound.isPlaying) loopSound.Stop();
        }
    }

    private void Burn()
    {
        GetFlameBox(out Vector2 center, out Vector2 size, out float angle);

        int count = Physics2D.OverlapBoxNonAlloc(center, size, angle, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D c = hitBuffer[i];
            if (c == null || c.transform.IsChildOf(transform)) continue;

            TestAnimal animal = c.GetComponentInParent<TestAnimal>();
            if (animal != null)
            {
                if (detectAnimals) HandleAnimal(animal);
                continue;
            }

            if (burnBoxes)
            {
                PushableBox box = c.GetComponentInParent<PushableBox>();
                if (box != null && !box.IsBroken)
                {
                    box.BreakBox();
                    onBoxBurned?.Invoke(box);
                    continue;
                }
            }

            if (burnDoors)
            {
                Door door = c.GetComponentInParent<Door>();
                if (door != null && !door.IsBroken && (door.IsBreakable || forceBurnDoors))
                {
                    door.BreakDoor();
                }
            }
        }
    }

    private void HandleAnimal(TestAnimal animal)
    {
        if (lastBurn.TryGetValue(animal, out float last) && Time.time - last < animalBurnCooldown) return;
        lastBurn[animal] = Time.time;

        Debug.Log($"[FlameThrower] {name} เผาโดน {animal.name}");
        onAnimalBurned?.Invoke(animal);

        if (resetPuzzleOnAnimalBurn)
        {
            var reset = FindFirstObjectByType<PuzzleResetManager>();
            if (reset != null) reset.ResetPuzzle();
        }
    }
    #endregion

    #region Geometry
    private Vector2 GetFireVector()
    {
        Vector2 v;
        switch (fireDirection)
        {
            case AutoDirection.Right: v = Vector2.right; break;
            case AutoDirection.Left: v = Vector2.left; break;
            case AutoDirection.Up: v = Vector2.up; break;
            case AutoDirection.Down: v = Vector2.down; break;
            case AutoDirection.UpRight: v = new Vector2(1f, 1f).normalized; break;
            case AutoDirection.UpLeft: v = new Vector2(-1f, 1f).normalized; break;
            case AutoDirection.DownRight: v = new Vector2(1f, -1f).normalized; break;
            default: v = new Vector2(-1f, -1f).normalized; break;
        }
        return Quaternion.Euler(0f, 0f, extraAngle) * v;
    }

    /// <summary>ความยาวไฟจริง (ถ้าตั้ง Block Layer แล้วชนกำแพงจะสั้นลง)</summary>
    private float GetEffectiveLength(Vector2 origin, Vector2 dir)
    {
        if (blockLayer.value == 0) return flameLength;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, flameLength, blockLayer);
        float best = flameLength;
        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.isTrigger || h.collider.transform.IsChildOf(transform)) continue;
            best = Mathf.Min(best, h.distance);
        }
        return Mathf.Max(best, 0.05f);
    }

    private void GetFlameBox(out Vector2 center, out Vector2 size, out float angle)
    {
        Vector2 dir = GetFireVector();
        Vector2 origin = (Vector2)transform.position + dir * startOffset;
        float len = GetEffectiveLength(origin, dir);

        center = origin + dir * (len * 0.5f);
        size = new Vector2(len, flameWidth);
        angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }
    #endregion

    #region Debug
    private void OnDrawGizmos()
    {
        Vector2 dir = GetFireVector();
        Vector2 origin = (Vector2)transform.position + dir * startOffset;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // กรอบเต็มระยะ (ที่ตั้งไว้ใน Inspector)
        Matrix4x4 old = Gizmos.matrix;
        Vector3 fullCenter = origin + dir * (flameLength * 0.5f);
        Gizmos.matrix = Matrix4x4.TRS(fullCenter, Quaternion.Euler(0f, 0f, angle), Vector3.one);
        bool live = Application.isPlaying && IsFiring;
        Gizmos.color = live ? new Color(1f, 0.35f, 0.05f, 0.35f) : new Color(1f, 0.45f, 0.1f, 0.15f);
        Gizmos.DrawCube(Vector3.zero, new Vector3(flameLength, flameWidth, 0.01f));
        Gizmos.color = new Color(1f, 0.4f, 0.05f, 0.95f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(flameLength, flameWidth, 0.01f));
        Gizmos.matrix = old;

        // ความยาวจริงเมื่อโดนกำแพงบัง (สีเหลือง)
        float effLen = Application.isPlaying || blockLayer.value != 0 ? GetEffectiveLength(origin, dir) : flameLength;
        if (effLen < flameLength - 0.01f)
        {
            Gizmos.matrix = Matrix4x4.TRS(origin + dir * (effLen * 0.5f), Quaternion.Euler(0f, 0f, angle), Vector3.one);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(effLen, flameWidth, 0.01f));
            Gizmos.matrix = old;
        }

        // ลูกศรทิศ + จุดเริ่มไฟ + ตัวบอกระยะ
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 1f);
        Gizmos.DrawLine(transform.position, origin);
        Gizmos.DrawWireSphere(origin, 0.08f);
        Vector3 tip = origin + dir * flameLength;
        Gizmos.DrawLine(origin, tip);
        Vector3 side = new Vector3(-dir.y, dir.x, 0f) * 0.2f;
        Gizmos.DrawLine(tip, tip - (Vector3)dir * 0.3f + side);
        Gizmos.DrawLine(tip, tip - (Vector3)dir * 0.3f - side);

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(1f, 0.85f, 0.3f, 1f);
        UnityEditor.Handles.Label(fullCenter + (Vector3)(new Vector2(-dir.y, dir.x) * (flameWidth * 0.5f + 0.15f)),
            $"Flame {flameLength:0.0} x {flameWidth:0.0}");
#endif
    }
    #endregion
}
