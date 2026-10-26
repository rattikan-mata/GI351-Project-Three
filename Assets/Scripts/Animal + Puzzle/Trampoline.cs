using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// เทรมโพลีน: วางสคริปต์นี้บนวัตถุที่มี Collider2D อะไรก็ตามที่มาแตะจะถูกดีดออกไปตามทิศ "บน" ของวัตถุนี้ (transform.up)
/// หมุนวัตถุเพื่อเปลี่ยนทิศดีดได้ ใช้ได้กับ สัตว์ / กล่อง / ก้อนขน(projectile) / Rigidbody2D อื่นๆ
/// ใช้ได้ทั้งแบบ Collider ตัน และแบบ Trigger
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Trampoline : MonoBehaviour
{
    #region Bounce
    [Header("Bounce (แรงและทิศดีด)")]
    [Tooltip("ความเร็วที่ดีดออกไปตามทิศบนของเทรมโพลีน (หน่วยต่อวินาที)")]
    [SerializeField, Min(0f)] private float bounceSpeed = 12f;

    [Tooltip("ติ๊กไว้ = ดีดเฉพาะของที่มาแตะจากด้านหน้า (ด้านบน) ของเทรมโพลีน / ไม่ติ๊ก = แตะด้านไหนก็ดีดทั้งหมด")]
    [SerializeField] private bool onlyFromFrontSide = true;

    [Tooltip("เวลาพักก่อนดีดของชิ้นเดิมซ้ำได้อีก (วินาที) กันดีดรัวหลายรอบ")]
    [SerializeField, Min(0f)] private float cooldownPerObject = 0.25f;
    #endregion

    #region Who Bounces
    [Header("Who Bounces (อะไรเด้งได้)")]
    [SerializeField] private bool bounceAnimals = true;
    [SerializeField] private bool bounceBoxes = true;
    [Tooltip("projectile ของสัตว์ เช่น ก้อนขน / ฟองสบู่")]
    [SerializeField] private bool bounceProjectiles = true;
    [Tooltip("Rigidbody2D แบบ Dynamic อื่นๆ ที่ไม่ใช่ของข้างบน")]
    [SerializeField] private bool bounceOtherBodies = true;

    [Tooltip("ติ๊กไว้ = สัตว์ที่กำลังบินอยู่ก็ถูกดีดด้วย (ตัวบินคุมความเร็วเองอยู่ อาจถูกทับ)")]
    [SerializeField] private bool bounceAirborneAnimals = false;
    #endregion

    #region Visual
    [Header("Visual (Optional)")]
    [Tooltip("(ไม่บังคับ) Transform ของภาพที่จะบีบตัวตอนดีด ว่างไว้ = ไม่บีบ (ไม่แนะนำให้ใส่ตัวหลักที่มี Collider)")]
    [SerializeField] private Transform visualTarget;

    [Tooltip("สัดส่วนที่บีบแกน Y ตอนดีด (1 = ไม่บีบ)")]
    [SerializeField, Range(0.3f, 1f)] private float squashScaleY = 0.7f;

    [SerializeField, Min(0.02f)] private float squashDuration = 0.18f;

    [Header("Audio (เสียง)")]
    [Tooltip("เสียงตอนเด้ง")]
    [SerializeField] private AudioClip bounceSound;
    [Range(0f, 1f)]
    [Tooltip("ระดับความดังของเสียงเด้ง (0 - 1)")]
    [SerializeField] private float bounceVolume = 1f;
    [SerializeField] private AudioSource audioSource;
    #endregion

    #region State
    private readonly Dictionary<GameObject, float> lastBounceTime = new Dictionary<GameObject, float>();
    private Vector3 visualBaseScale = Vector3.one;
    private Coroutine squashRoutine;

    /// <summary>ทิศที่ดีดออกไป (ทิศบนของวัตถุ)</summary>
    public Vector2 BounceDirection => transform.up;
    public float BounceSpeed
    {
        get => bounceSpeed;
        set => bounceSpeed = Mathf.Max(0f, value);
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (visualTarget != null) visualBaseScale = visualTarget.localScale;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void OnDisable()
    {
        if (squashRoutine != null)
        {
            StopCoroutine(squashRoutine);
            squashRoutine = null;
        }
        if (visualTarget != null) visualTarget.localScale = visualBaseScale;
    }

    private void OnCollisionEnter2D(Collision2D collision) => TryBounce(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => TryBounce(collision.collider);
    private void OnTriggerEnter2D(Collider2D other) => TryBounce(other);
    private void OnTriggerStay2D(Collider2D other) => TryBounce(other);
    #endregion

    #region Logic
    private void TryBounce(Collider2D other)
    {
        if (other == null || other.gameObject == gameObject) return;

        Vector2 up = transform.up;
        Vector2 launch = up * bounceSpeed;

        // กำหนดชนิดของที่มาแตะ (เรียงลำดับ: สัตว์ > กล่อง > ก้อนขน > Rigidbody อื่น)
        TestAnimal animal = other.GetComponentInParent<TestAnimal>();
        PushableBox box = animal == null ? other.GetComponentInParent<PushableBox>() : null;
        Hairball hairball = (animal == null && box == null) ? other.GetComponentInParent<Hairball>() : null;
        Rigidbody2D body = other.attachedRigidbody != null ? other.attachedRigidbody : other.GetComponentInParent<Rigidbody2D>();

        GameObject target;
        if (animal != null)
        {
            if (!bounceAnimals) return;
            if (animal.IsAirborne && !bounceAirborneAnimals) return;
            target = animal.gameObject;
        }
        else if (box != null)
        {
            if (!bounceBoxes || box.IsBroken) return;
            target = box.gameObject;
        }
        else if (hairball != null)
        {
            if (!bounceProjectiles) return;
            target = hairball.gameObject;
        }
        else if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
        {
            if (!bounceOtherBodies) return;
            target = body.gameObject;
        }
        else
        {
            return;
        }

        if (onlyFromFrontSide)
        {
            Vector2 rel = (Vector2)other.bounds.center - (Vector2)transform.position;
            if (Vector2.Dot(rel, up) < 0f) return;
        }

        if (lastBounceTime.TryGetValue(target, out float last) && Time.time - last < cooldownPerObject) return;
        lastBounceTime[target] = Time.time;

        if (animal != null)
        {
            Rigidbody2D arb = animal.GetComponent<Rigidbody2D>();
            if (arb != null) arb.linearVelocity = launch;
        }
        else if (box != null)
        {
            box.Launch(launch);
        }
        else if (hairball != null)
        {
            hairball.Launch(launch);
        }
        else
        {
            body.linearVelocity = launch;
        }

        PlayFeedback();
    }

    private void PlayFeedback()
    {
        AudioClip clip = bounceSound != null ? bounceSound : (AudioManager.Instance != null ? AudioManager.Instance.GetClip("TrampolineBounce") : null);
        if (clip != null)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFXAt(clip, transform.position, bounceVolume);
            else if (audioSource != null) audioSource.PlayOneShot(clip, bounceVolume);
            else AudioSource.PlayClipAtPoint(clip, transform.position, bounceVolume);
        }

        if (visualTarget != null)
        {
            if (squashRoutine != null) StopCoroutine(squashRoutine);
            squashRoutine = StartCoroutine(SquashRoutine());
        }
    }

    private IEnumerator SquashRoutine()
    {
        float half = squashDuration * 0.5f;
        Vector3 squashed = new Vector3(visualBaseScale.x * (2f - squashScaleY), visualBaseScale.y * squashScaleY, visualBaseScale.z);

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            visualTarget.localScale = Vector3.Lerp(visualBaseScale, squashed, Mathf.Clamp01(t / half));
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            visualTarget.localScale = Vector3.Lerp(squashed, visualBaseScale, Mathf.Clamp01(t / half));
            yield return null;
        }

        visualTarget.localScale = visualBaseScale;
        squashRoutine = null;
    }
    #endregion

    #region Debug
    private void OnDrawGizmos()
    {
        Vector3 origin = transform.position;
        Vector3 dir = transform.up;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Bounds b = col.bounds;
            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.15f);
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireCube(b.center, b.size);
            origin = b.center;
        }

        // ความยาวลูกศร = ประมาณความสูงที่ดีดได้ (v^2 / 2g) เพื่อให้เห็นภาพคร่าวๆ
        float g = Mathf.Max(0.01f, Mathf.Abs(Physics2D.gravity.y));
        float height = (bounceSpeed * bounceSpeed) / (2f * g);
        Gizmos.color = new Color(0.4f, 1f, 0.7f, 1f);
        Vector3 tip = origin + dir * Mathf.Max(0.5f, height);
        Gizmos.DrawLine(origin, tip);
        Vector3 side = new Vector3(-dir.y, dir.x, 0f) * 0.2f;
        Gizmos.DrawLine(tip, tip - dir * 0.3f + side);
        Gizmos.DrawLine(tip, tip - dir * 0.3f - side);
    }
    #endregion
}
