using UnityEngine;

/// <summary>
/// สปริงมือแมวที่ผลักของ: ฝังอยู่ในกำแพง ปกติโผล่ออกมานิดเดียวแล้วขยับแอบๆ (Peek)
/// พอสวิตช์ถูกกด (เปิด) สปริงจะเลื่อนยื่นออกมาไกลขึ้นและผลักกล่อง / สัตว์ที่ขวางหน้าไป ปิดสวิตช์แล้วเลื่อนกลับเข้ากำแพง
/// ภาพ "เลื่อน" ออกมาจากช่องในกำแพงเฉยๆ ไม่ยืดไม่บี้ภาพ (ส่วนที่ยังอยู่ในกำแพงถูกซ่อนด้วย Sprite Mask)
/// ทิศที่ผลัก = แกน X ของตัว Prefab (หมุน Prefab เพื่อเปลี่ยนทิศ: 0 = ขวา, 90 = ขึ้น, 180 = ซ้าย, 270 = ลง)
/// - กล่อง (PushableBox): ผลักผ่าน TryPush และหยุดเมื่อกล่องติดกำแพง
/// - สัตว์: ตัวมือเป็น Rigidbody2D แบบ Kinematic เลื่อนชน สัตว์จึงถูกดันตามฟิสิกส์
/// - ถ้าเจอกำแพงหรือของที่ดันไม่ไป มือจะหยุดแค่นั้น (ไม่ทะลุ)
/// </summary>
public class SpringPusher : MonoBehaviour
{
    #region Switch Link
    [Header("Switch Link (สวิตช์ที่สั่งให้สปริงผลัก)")]
    [Tooltip("ลาก FloorSwitch จากฉากมาใส่ เปิดสวิตช์ = สปริงยื่นออกไปผลัก ปิดสวิตช์ = หดกลับ\nว่างไว้ = สปริงโผล่แอบๆ อยู่เฉยๆ ไม่ผลัก")]
    [SerializeField] private FloorSwitch[] switches;

    [Tooltip("ติ๊กไว้ = ต้องเปิดสวิตช์ครบทุกตัว / ไม่ติ๊ก = ตัวใดตัวหนึ่งก็พอ")]
    [SerializeField] private bool requireAll = true;

    [Tooltip("ติ๊กไว้ = สวิตช์เปิดครั้งเดียวแล้วสปริงค้างยื่นไว้ ไม่หดกลับแม้ปิดสวิตช์")]
    [SerializeField] private bool stayOnOnceActivated = false;

    [Tooltip("มากกว่า 0 = ยื่นค้างไว้เท่านี้ (วินาที) แล้วหดกลับเองทั้งที่สวิตช์ยังเปิดอยู่ (ผลักทีเดียวแล้วกลับ) 0 = ยื่นค้างจนปิดสวิตช์")]
    [SerializeField, Min(0f)] private float autoRetractSeconds = 0f;
    #endregion

    #region Parts
    [Header("Parts (ชิ้นส่วนใน Prefab)")]
    [Tooltip("ตัวที่เลื่อนเข้าออก (มี Rigidbody2D Kinematic + Collider ของมือแมว)")]
    [SerializeField] private Rigidbody2D sliderBody;

    [Tooltip("ตัวที่ขยับแอบๆ ตอนอยู่นิ่ง (ลูกของ Slider ที่เก็บภาพสปริงกับมือแมว)")]
    [SerializeField] private Transform visual;

    [Tooltip("Collider ของมือแมว (ใช้คำนวณพื้นที่ผลัก)")]
    [SerializeField] private BoxCollider2D pawCollider;

    [Tooltip("ตำแหน่ง X ของ Slider (หน่วย local) ตอนที่ยื่นออกมา 0 = ซ่อนสนิท ปรับตามภาพ ถ้าเปลี่ยนภาพ")]
    [SerializeField] private float hiddenLocalX = -0.405f;
    #endregion

    #region Travel
    [Header("Travel (ระยะยื่น นับจากขอบแผ่นเหล็กถึงปลายมือ หน่วย Unity)")]
    [Tooltip("ระยะที่โผล่ตอนอยู่นิ่ง (เห็นนิดเดียว)")]
    [SerializeField, Min(0f)] private float idleExtension = 0.12f;

    [Tooltip("ระยะที่ยื่นสุดตอนสวิตช์เปิด (ภาพเลื่อนอย่างเดียว ไม่ยืดภาพ ยื่นเกิน 1.27 จะเห็นสปริงขาดจากแผ่นเหล็ก)")]
    [SerializeField, Min(0.1f)] private float activeExtension = 1.2f;

    [Tooltip("ความเร็วตอนยื่นออกไปผลัก (หน่วยต่อวินาที)")]
    [SerializeField, Min(0.1f)] private float extendSpeed = 6f;

    [Tooltip("ความเร็วตอนหดกลับ (หน่วยต่อวินาที)")]
    [SerializeField, Min(0.1f)] private float retractSpeed = 2.5f;
    #endregion

    #region Peek
    [Header("Idle Peek (ขยับแอบๆ ตอนอยู่นิ่ง)")]
    [Tooltip("ระยะที่มือโผล่เข้าออกตอนแอบ (หน่วย Unity) 0 = ไม่ขยับ")]
    [SerializeField, Min(0f)] private float peekAmplitude = 0.08f;

    [Tooltip("ความเร็วการแอบ (รอบต่อวินาที x 2π)")]
    [SerializeField, Min(0f)] private float peekSpeed = 3f;
    #endregion

    #region Push
    [Header("Push (ผลักอะไรได้บ้าง)")]
    [SerializeField] private bool pushBoxes = true;

    [Tooltip("กล่องไถลต่ออีกเท่านี้หลังมือหยุด (หน่วย Unity) ให้ดูถูกกระแทกออกไป")]
    [SerializeField, Min(0f)] private float boxFollowThrough = 0.3f;

    [Tooltip("ระยะมองไปข้างหน้าหาของที่จะผลัก (หน่วย Unity)")]
    [SerializeField, Min(0.05f)] private float lookAhead = 0.3f;

    [SerializeField] private float skinWidth = 0.02f;
    #endregion

    #region Audio
    [Header("Audio (ไม่บังคับ)")]
    [SerializeField] private AudioClip extendSound;
    [SerializeField] private AudioClip retractSound;
    [Range(0f, 1f)] [SerializeField] private float soundVolume = 1f;
    #endregion

    #region State
    public bool IsPowered { get; private set; }
    public float Extension => extension;

    private float extension;
    private float poweredFor;
    private float sliderLocalY;
    private bool extending;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (sliderBody == null) sliderBody = GetComponentInChildren<Rigidbody2D>();
        if (sliderBody != null)
        {
            sliderBody.bodyType = RigidbodyType2D.Kinematic;
            sliderBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            sliderLocalY = transform.InverseTransformPoint(sliderBody.transform.position).y;
        }
        if (pawCollider == null) pawCollider = GetComponentInChildren<BoxCollider2D>();

        // หันซ้าย: หมุน 180 จะกลับหัวภาพ พลิกแกน Y ให้ภาพตั้งตรงเหมือนกระจก
        if (transform.right.x < -0.5f)
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
                sr.flipY = true;
        }

        extension = idleExtension;
        ApplyPose(1f, true);
    }

    private void OnEnable()
    {
        if (switches == null) return;
        foreach (var sw in switches)
            if (sw != null) sw.StateChanged += OnSwitchChanged;
    }

    private void OnDisable()
    {
        if (switches == null) return;
        foreach (var sw in switches)
            if (sw != null) sw.StateChanged -= OnSwitchChanged;
    }

    private void Start()
    {
        Evaluate();
    }

    private void FixedUpdate()
    {
        Evaluate();
        if (IsPowered) poweredFor += Time.fixedDeltaTime;

        bool wantOut = IsPowered && (autoRetractSeconds <= 0f || poweredFor < autoRetractSeconds);
        float target = wantOut ? activeExtension : idleExtension;
        float dt = Time.fixedDeltaTime;

        if (target > extension)
        {
            if (!extending) { extending = true; PlaySound(extendSound); }
            Extend(target, dt);
        }
        else if (target < extension)
        {
            if (extending) { extending = false; PlaySound(retractSound); }
            extension = Mathf.MoveTowards(extension, target, retractSpeed * dt);
        }

        ApplyPose(wantOut ? 0f : 1f);
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
        poweredFor = 0f;
    }

    private bool ShouldRun()
    {
        if (switches == null || switches.Length == 0) return false;

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
        if (!hasAny) return false;
        return requireAll ? all : any;
    }

    /// <summary>ยื่นมือออกไปทีละก้าว ผลักกล่องที่เจอ และหยุดเมื่อชนกำแพงหรือของที่ดันไม่ไป</summary>
    private void Extend(float target, float dt)
    {
        float step = Mathf.Min(extendSpeed * dt, target - extension);
        Vector2 dir = transform.right;
        float allowed = step;

        GetPawBox(extension, out Vector2 center, out Vector2 size, out float angle);
        int count = Physics2D.BoxCastNonAlloc(center, size, angle, dir, hits, step + lookAhead);
        for (int i = 0; i < count; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null || col.isTrigger || col.transform.IsChildOf(transform)) continue;

            // พื้นผิวที่ไม่ได้ขวางทางเลื่อน (เช่น พื้นที่มือเฉียดอยู่) ไม่นับ
            if (hits[i].normal.sqrMagnitude > 0.001f && Vector2.Dot(dir, hits[i].normal) > -0.15f) continue;

            // สัตว์ / ก้อนขน: ปล่อยให้ฟิสิกส์ดันตามตัวมือ
            if (col.GetComponentInParent<TestAnimal>() != null || col.GetComponentInParent<Hairball>() != null) continue;

            var box = col.GetComponentInParent<PushableBox>();
            if (box != null && pushBoxes && !box.IsBroken)
            {
                if (box.GetFreeDistance(dir, 0.15f) >= 0.05f)
                {
                    box.TryPush(dir, (target - extension) + boxFollowThrough, extendSpeed * 1.1f);
                    continue;
                }
            }

            // กำแพง ประตู หรือกล่องที่ติดอยู่ ขวางอยู่
            allowed = Mathf.Min(allowed, Mathf.Max(0f, hits[i].distance - skinWidth));
        }

        extension += allowed;
    }
    #endregion

    #region Pose
    private void ApplyPose(float peekBlend, bool snap = false)
    {
        if (sliderBody != null)
        {
            Vector3 world = transform.TransformPoint(new Vector3(hiddenLocalX + extension, sliderLocalY, 0f));
            if (snap || !Application.isPlaying)
            {
                // ตั้งท่าทันที (ตอนเริ่ม) ไม่ให้เห็นท่าที่บันทึกใน Prefab วูบเดียว
                sliderBody.transform.position = world;
                sliderBody.position = world;
            }
            else sliderBody.MovePosition(world);
        }

        if (visual != null)
        {
            // แอบเข้าๆ ออกๆ เฉพาะตอนอยู่นิ่ง (ภาพเลื่อนล้ำหน้า Collider ได้ไม่เกิน Peek Amplitude)
            float wave = (0.5f + 0.5f * Mathf.Sin(Time.time * peekSpeed)) * peekAmplitude * peekBlend;
            visual.localPosition = new Vector3(wave, 0f, 0f);
        }
    }

    private void GetPawBox(float ext, out Vector2 center, out Vector2 size, out float angle)
    {
        Vector2 off = pawCollider != null ? pawCollider.offset : Vector2.zero;
        Vector2 sz = pawCollider != null ? pawCollider.size : new Vector2(0.8f, 0.66f);
        Vector3 lossy = transform.lossyScale;

        center = transform.TransformPoint(new Vector3(hiddenLocalX + ext + off.x, sliderLocalY + off.y, 0f));
        size = new Vector2(sz.x * Mathf.Abs(lossy.x), sz.y * Mathf.Abs(lossy.y));
        angle = transform.eulerAngles.z;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFXAt(clip, transform.position, soundVolume);
        else AudioSource.PlayClipAtPoint(clip, transform.position, soundVolume);
    }
    #endregion

    #region Editor Preview
    // ในโหมดแก้ไขสคริปต์ไม่ทำงาน ใช้เมนู (คลิกขวาที่หัวคอมโพเนนต์) เลื่อนภาพดูท่าต่างๆ ได้ ค่าจริงตอนเล่นไม่เกี่ยว
    [ContextMenu("Preview: โผล่นิดเดียว (Idle)")]
    private void PreviewIdle() => PreviewAt(idleExtension);

    [ContextMenu("Preview: ยื่นสุด (Extended)")]
    private void PreviewExtended() => PreviewAt(activeExtension);

    private void PreviewAt(float ext)
    {
        if (Application.isPlaying || sliderBody == null) return;
        Transform t = sliderBody.transform;
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(t, "Spring Pusher Preview");
#endif
        t.localPosition = new Vector3(hiddenLocalX + ext, t.localPosition.y, 0f);
        if (visual != null) visual.localPosition = Vector3.zero;
    }
    #endregion

    #region Debug
    private void OnDrawGizmosSelected()
    {
        if (pawCollider == null) pawCollider = GetComponentInChildren<BoxCollider2D>();
        float y = sliderBody != null ? transform.InverseTransformPoint(sliderBody.transform.position).y : 0f;
        Vector2 off = pawCollider != null ? pawCollider.offset : Vector2.zero;
        Vector2 sz = pawCollider != null ? pawCollider.size : new Vector2(0.8f, 0.66f);

        DrawPaw(idleExtension, y, off, sz, new Color(1f, 0.85f, 0.3f, 0.9f));
        DrawPaw(activeExtension, y, off, sz, new Color(1f, 0.4f, 0.2f, 0.9f));

        Gizmos.color = new Color(1f, 0.85f, 0.3f, 1f);
        Vector3 a = transform.TransformPoint(new Vector3(hiddenLocalX + idleExtension + off.x, y + off.y, 0f));
        Vector3 b = transform.TransformPoint(new Vector3(hiddenLocalX + activeExtension + off.x, y + off.y, 0f));
        Gizmos.DrawLine(a, b);
    }

    private void DrawPaw(float ext, float y, Vector2 off, Vector2 sz, Color color)
    {
        Matrix4x4 old = Gizmos.matrix;
        Vector3 c = transform.TransformPoint(new Vector3(hiddenLocalX + ext + off.x, y + off.y, 0f));
        Gizmos.matrix = Matrix4x4.TRS(c, transform.rotation, transform.lossyScale);
        Gizmos.color = color;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(sz.x, sz.y, 0.01f));
        Gizmos.matrix = old;
    }
    #endregion
}
