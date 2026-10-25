using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สายพาน: วางสคริปต์นี้บนวัตถุที่มี Collider2D แล้วลาก FloorSwitch ใส่ช่อง Switches
/// ตอนสวิตช์ทำงาน สายพานจะพา กล่อง / แมว / ก้อนขน ไปตามทิศที่ตั้งไว้
/// ถ้าไม่ใส่สวิตช์เลย สายพานจะทำงานตลอด (ใช้ทดสอบได้)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ConveyorBelt : MonoBehaviour
{
    #region Switch Link
    [Header("Switch Link (สวิตช์ที่เปิดสายพาน)")]
    [Tooltip("ลาก FloorSwitch จากฉากมาใส่ ตัวไหนที่ต้องการให้เปิดสายพานนี้")]
    [SerializeField] private FloorSwitch[] switches;

    [Tooltip("ติ๊กไว้ = ต้องกดสวิตช์ครบทุกตัวพร้อมกัน / ไม่ติ๊ก = ตัวใดตัวหนึ่งก็พอ")]
    [SerializeField] private bool requireAll = true;

    [Tooltip("ติ๊กไว้ = เปิดแล้วสายพานทำงานต่อเนื่อง แม้สวิตช์จะปล่อยแล้ว")]
    [SerializeField] private bool stayOnOnceActivated = false;
    #endregion

    #region Motion
    [Header("Motion (ทิศและความเร็ว)")]
    [Tooltip("ทิศที่ของบนสายพานถูกพาไป")]
    [SerializeField] private AutoDirection moveDirection = AutoDirection.Right;

    [Tooltip("ความเร็วที่พาของไป (หน่วยต่อวินาที)")]
    [SerializeField] private float speed = 2f;

    [Tooltip("Layer ที่ขวางของบนสายพาน (กำแพง ประตู ฯลฯ) เว้นว่าง = ไม่เช็คชน")]
    [SerializeField] private LayerMask obstacleLayer;

    [SerializeField] private float skinWidth = 0.02f;
    #endregion

    #region Who Moves
    [Header("Who Moves (อะไรถูกพาได้)")]
    [SerializeField] private bool moveAnimals = true;
    [SerializeField] private bool moveBoxes = true;
    [SerializeField] private bool moveHairballs = true;

    [Tooltip("ติ๊กไว้ = แมวที่กำลังบินอยู่จะไม่ถูกสายพานพา")]
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

    #region Audio
    [Header("Audio (เสียง)")]
    [Tooltip("เสียงตอนสายพานทำงาน (จะเล่นวนลูปอัตโนมัติเมื่อสายพานเปิด)")]
    [SerializeField] private AudioClip runningSound;

    [Tooltip("AudioSource สำหรับเล่นเสียง (ถ้าเว้นว่างจะค้นหาจากตัวสายพานให้อัตโนมัติ)")]
    [SerializeField] private AudioSource audioSource;
    #endregion

    #region State
    public bool IsPowered { get; private set; }

    private readonly HashSet<Collider2D> occupants = new HashSet<Collider2D>();
    private readonly List<Collider2D> removeBuffer = new List<Collider2D>();
    private readonly RaycastHit2D[] castResults = new RaycastHit2D[8];
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null && runningSound != null)
        {
            audioSource.clip = runningSound;
            audioSource.loop = true;
        }
    }

    private void OnEnable()
    {
        foreach (var sw in switches)
        {
            if (sw != null) sw.StateChanged += OnSwitchChanged;
        }
    }

    private void OnDisable()
    {
        foreach (var sw in switches)
        {
            if (sw != null) sw.StateChanged -= OnSwitchChanged;
        }
        ClearAnimalCarry();
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private void Start()
    {
        Evaluate();
        ApplyVisual();
        UpdateAudio();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsValidRider(other)) return;
        occupants.Add(other);
        var box = other.GetComponentInParent<PushableBox>();
        if (box != null) box.AddGravityHold();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        occupants.Remove(other);
        var animal = other.GetComponentInParent<TestAnimal>();
        if (animal != null) animal.ConveyorVelocity = Vector2.zero;
        var box = other.GetComponentInParent<PushableBox>();
        if (box != null) box.RemoveGravityHold();
    }

    private void FixedUpdate()
    {
        CleanupOccupants();
        if (!IsPowered)
        {
            ClearAnimalCarry();
            return;
        }

        Vector2 dir = ToDirection(moveDirection);
        Vector2 animalCarry = dir * speed;
        float step = speed * Time.fixedDeltaTime;

        foreach (var col in occupants)
        {
            if (col == null) continue;

            var animal = col.GetComponentInParent<TestAnimal>();
            if (animal != null)
            {
                if (moveAnimals && !(ignoreAirborneAnimals && animal.IsAirborne))
                    animal.ConveyorVelocity = animalCarry;
                else
                    animal.ConveyorVelocity = Vector2.zero;
                continue;
            }

            var rb = col.attachedRigidbody;
            if (rb == null) rb = col.GetComponentInParent<Rigidbody2D>();
            if (rb == null) continue;

            if (moveBoxes && col.GetComponentInParent<PushableBox>() != null)
            {
                MoveKinematic(rb, col, dir, step);
                continue;
            }

            if (moveHairballs && col.GetComponentInParent<Hairball>() != null)
                MoveKinematic(rb, col, dir, step);
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
        UpdateAudio();
        if (!IsPowered) ClearAnimalCarry();
    }

    private void UpdateAudio()
    {
        if (audioSource == null) return;

        if (runningSound == null && AudioManager.Instance != null)
            runningSound = AudioManager.Instance.GetClip("BeltRunning");

        if (runningSound != null && audioSource.clip != runningSound)
        {
            audioSource.clip = runningSound;
            audioSource.loop = true;
        }

        if (IsPowered && runningSound != null)
        {
            float targetVol = (AudioManager.Instance != null)
                ? (AudioManager.Instance.IsMuted ? 0f : AudioManager.Instance.MasterVolume * AudioManager.Instance.SfxVolume)
                : 1f;
            audioSource.volume = targetVol;

            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
        else
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
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

    private bool IsValidRider(Collider2D c)
    {
        if (moveAnimals && c.GetComponentInParent<TestAnimal>() != null) return true;
        if (moveBoxes && c.GetComponentInParent<PushableBox>() != null) return true;
        if (moveHairballs && c.GetComponentInParent<Hairball>() != null) return true;
        return false;
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

    private void CleanupOccupants()
    {
        removeBuffer.Clear();
        foreach (var c in occupants)
        {
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                removeBuffer.Add(c);
        }
        foreach (var c in removeBuffer)
        {
            occupants.Remove(c);
            if (c != null)
            {
                var animal = c.GetComponentInParent<TestAnimal>();
                if (animal != null) animal.ConveyorVelocity = Vector2.zero;
                var box = c.GetComponentInParent<PushableBox>();
                if (box != null) box.RemoveGravityHold();
            }
        }
    }

    private void ClearAnimalCarry()
    {
        foreach (var c in occupants)
        {
            if (c == null) continue;
            var animal = c.GetComponentInParent<TestAnimal>();
            if (animal != null) animal.ConveyorVelocity = Vector2.zero;
        }
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
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position;
        Vector3 dir = (Vector3)ToDirection(moveDirection);
        Gizmos.DrawLine(origin, origin + dir * 1.5f);
        Gizmos.DrawSphere(origin + dir * 1.5f, 0.08f);
    }
    #endregion
}
